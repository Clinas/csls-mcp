using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace csls_mcp;

/// <summary>
/// Handles incoming Model Context Protocol (MCP) requests and dispatches them to the appropriate Roslyn-based tools.
/// </summary>
public class McpHandler
{
    private readonly Solution _solution;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public McpHandler(Solution solution, JsonSerializerOptions jsonSerializerOptions)
    {
        _solution = solution ?? throw new ArgumentNullException(nameof(solution));
        _jsonSerializerOptions = jsonSerializerOptions;
    }

    public async Task<McpMessage?> HandleRequest(McpRequest request)
    {
        if (request.Method == "notifications/initialized")
        {
            // Notifications do not get a response.
            return null;
        }

        try
        {
            var response = await RouteRequest(request);
            response.Id = request.Id;
            return response;
        }
        catch (JsonException ex)
        {
            return new JsonRpcErrorResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32700, Message = "Invalid JSON input for tool", Data = ex.Message }
            };
        }
        catch (Exception ex)
        {
            return new JsonRpcErrorResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32000, Message = "Server error during tool execution", Data = ex.ToString() }
            };
        }
    }

    private async Task<McpMessage> RouteRequest(McpRequest request)
    {
        return request.Method switch
        {
            "initialize" => new JsonRpcResultResponse<InitializeResult>
            {
                Result = Initialize(request.Params.Deserialize<InitializeParams>(_jsonSerializerOptions))
            },
            "tools/list" => new JsonRpcResultResponse<GetToolDeclarationsOutput>
            {
                Result = new() { Tools = GetToolDeclarations() }
            },
            // This is a legacy/alternative name for tools/list.
            "getToolDeclarations" => new JsonRpcResultResponse<GetToolDeclarationsOutput> 
            {
                Result = new() { Tools = GetToolDeclarations() }
            },
            "tools/call" => new JsonRpcResultResponse<ToolResult>
            {
                Result = await HandleToolCall(request.Params.Deserialize<ToolCallParams>(_jsonSerializerOptions))
            },
            // Direct tool calls are supported for simplicity, but tools/call is preferred.
            "resolveSymbol" => new JsonRpcResultResponse<ToolResult> { Result = await ResolveSymbol(request.Params.Deserialize<SymbolInput>(_jsonSerializerOptions)) },
            "getSymbolSource" => new JsonRpcResultResponse<ToolResult> { Result = await GetSymbolSource(request.Params.Deserialize<SymbolInput>(_jsonSerializerOptions)) },
            "findReferences" => new JsonRpcResultResponse<ToolResult> { Result = await FindReferences(request.Params.Deserialize<SymbolInput>(_jsonSerializerOptions)) },
            "findImplementations" => new JsonRpcResultResponse<ToolResult> { Result = await FindImplementations(request.Params.Deserialize<SymbolInput>(_jsonSerializerOptions)) },
            "listMembers" => new JsonRpcResultResponse<ToolResult> { Result = await ListMembers(request.Params.Deserialize<SymbolInput>(_jsonSerializerOptions)) },
            _ => throw new InvalidOperationException($"Unknown method: {request.Method}")
        };
    }

    private async Task<ToolResult> HandleToolCall(ToolCallParams? toolCallParams)
    {
        if (toolCallParams == null)
        {
            throw new JsonException("Invalid parameters for tools/call");
        }

        return toolCallParams.Name switch
        {
            "resolveSymbol" => await ResolveSymbol(toolCallParams.Arguments.Deserialize<SymbolInput>(_jsonSerializerOptions)),
            "getSymbolSource" => await GetSymbolSource(toolCallParams.Arguments.Deserialize<SymbolInput>(_jsonSerializerOptions)),
            "findReferences" => await FindReferences(toolCallParams.Arguments.Deserialize<SymbolInput>(_jsonSerializerOptions)),
            "findImplementations" => await FindImplementations(toolCallParams.Arguments.Deserialize<SymbolInput>(_jsonSerializerOptions)),
            "listMembers" => await ListMembers(toolCallParams.Arguments.Deserialize<SymbolInput>(_jsonSerializerOptions)),
            _ => throw new InvalidOperationException($"Unknown tool: {toolCallParams.Name}")
        };
    }

    private InitializeResult Initialize(InitializeParams? args)
    {
        var tools = GetToolDeclarations();
        var toolDictionary = tools.ToDictionary(t => t.Name, t => t);

        return new InitializeResult
        {
            ProtocolVersion = "2025-11-25",
            ServerInfo = new ServerInfo { Name = "csls-mcp", Version = "0.1.0" },
            Capabilities = new ServerCapabilities
            {
                Roots = new RootCapabilities { ListChanged = true },
                Tools = toolDictionary
            }
        };
    }

    private List<ToolDeclaration> GetToolDeclarations()
    {
        return new List<ToolDeclaration>
        {
            new() {
                Name = "resolveSymbol",
                Description = "Semantically resolves a C# symbol and returns its definition location.",
                InputSchema = JsonNode.Parse("{\"type\":\"object\",\"properties\":{\"symbol\":{\"type\":\"string\"}},\"required\":[\"symbol\"]}")!.AsObject()
            },
            new() {
                Name = "getSymbolSource",
                Description = "Retrieves the source code block for a C# symbol.",
                InputSchema = JsonNode.Parse("{\"type\":\"object\",\"properties\":{\"symbol\":{\"type\":\"string\"}},\"required\":[\"symbol\"]}")!.AsObject()
            },
            new() {
                Name = "findReferences",
                Description = "Finds all references to a C# symbol.",
                InputSchema = JsonNode.Parse("{\"type\":\"object\",\"properties\":{\"symbol\":{\"type\":\"string\"},\"page\":{\"type\":\"integer\"},\"pageSize\":{\"type\":\"integer\"}},\"required\":[\"symbol\"]}")!.AsObject()
            },
            new() {
                Name = "findImplementations",
                Description = "Finds all implementations of a C# interface.",
                InputSchema = JsonNode.Parse("{\"type\":\"object\",\"properties\":{\"symbol\":{\"type\":\"string\"},\"page\":{\"type\":\"integer\"},\"pageSize\":{\"type\":\"integer\"}},\"required\":[\"symbol\"]}")!.AsObject()
            },
            new() {
                Name = "listMembers",
                Description = "Lists members of a C# type.",
                InputSchema = JsonNode.Parse("{\"type\":\"object\",\"properties\":{\"symbol\":{\"type\":\"string\"}},\"required\":[\"symbol\"]}")!.AsObject()
            }
        };
    }
    
    private async Task<ToolResult> ResolveSymbol(SymbolInput? input)
    {
        if (input == null) throw new JsonException("Input for ResolveSymbol is null.");
        var cancellationToken = CancellationToken.None;

        var foundSymbols = await SymbolFinder.FindSourceDeclarationsAsync(_solution, name => name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase), cancellationToken);
        var symbol = foundSymbols.FirstOrDefault(s => s.Name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase)) ?? foundSymbols.FirstOrDefault();

        if (symbol?.Locations.FirstOrDefault(loc => loc.IsInSource) is { } location && location.SourceTree != null)
        {
            var lineSpan = location.SourceTree.GetLineSpan(location.SourceSpan, cancellationToken);
            var result = new
            {
                kind = symbol.Kind.ToString(),
                name = symbol.Name,
                @namespace = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                file = location.SourceTree.FilePath,
                line = lineSpan.StartLinePosition.Line + 1
            };
            return new ToolResult { Content = new List<McpContent> { new McpTextContent { Text = JsonSerializer.Serialize(result) } } };
        }

        return new ToolResult
        {
            IsError = true,
            Content = new List<McpContent> { new McpTextContent { Text = $"Error: Symbol '{input.Symbol}' not found." } }
        };
    }

    private async Task<ToolResult> GetSymbolSource(SymbolInput? input)
    {
        if (input == null) throw new JsonException("Input for GetSymbolSource is null.");
        var cancellationToken = CancellationToken.None;

        var foundSymbols = await SymbolFinder.FindSourceDeclarationsAsync(_solution, name => name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase), cancellationToken);
        var symbol = foundSymbols.FirstOrDefault(s => s.Name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase)) ?? foundSymbols.FirstOrDefault();

        if (symbol?.DeclaringSyntaxReferences.FirstOrDefault() is { } declaringReference)
        {
            var syntaxNode = await declaringReference.GetSyntaxAsync(cancellationToken);
            return new ToolResult { Content = new List<McpContent> { new McpTextContent { Text = syntaxNode.ToFullString() } } };
        }
        
        return new ToolResult
        {
            IsError = true,
            Content = new List<McpContent> { new McpTextContent { Text = $"Error: Source for symbol '{input.Symbol}' not found." } }
        };
    }

    private async Task<ToolResult> FindReferences(SymbolInput? input)
    {
        if (input == null) throw new JsonException("Input for FindReferences is null.");
        var cancellationToken = CancellationToken.None;
        var locations = new List<Location>();

        var foundSymbols = await SymbolFinder.FindSourceDeclarationsAsync(_solution, name => name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase), cancellationToken);
        var symbol = foundSymbols.FirstOrDefault(s => s.Name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase)) ?? foundSymbols.FirstOrDefault();

        if (symbol != null)
        {
            var symbolReferences = await SymbolFinder.FindReferencesAsync(symbol, _solution, cancellationToken);
            foreach (var reference in symbolReferences)
            {
                foreach (var loc in reference.Locations)
                {
                    var text = await loc.Document.GetTextAsync(cancellationToken);
                    var lineSpan = text.Lines.GetLinePosition(loc.Location.SourceSpan.Start);
                    locations.Add(new Location { File = loc.Document.FilePath ?? "Unknown", Line = lineSpan.Line + 1 });
                }
            }
        }
        
        return CreatePaginatedResult(locations, input.Page, input.PageSize);
    }
    
    private async Task<ToolResult> FindImplementations(SymbolInput? input)
    {
        if (input == null) throw new JsonException("Input for FindImplementations is null.");
        var cancellationToken = CancellationToken.None;
        var locations = new List<Location>();

        var foundSymbols = await SymbolFinder.FindSourceDeclarationsAsync(_solution, name => name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase), cancellationToken);
        var symbol = foundSymbols.FirstOrDefault(s => s.Name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase)) ?? foundSymbols.FirstOrDefault();

        if (symbol is INamedTypeSymbol interfaceSymbol)
        {
            var implementationSymbols = await SymbolFinder.FindImplementationsAsync(interfaceSymbol, _solution, cancellationToken: cancellationToken);
            foreach (var impl in implementationSymbols)
            {
                if (impl.Locations.FirstOrDefault(loc => loc.IsInSource) is { } location && location.SourceTree != null)
                {
                    var lineSpan = location.SourceTree.GetLineSpan(location.SourceSpan, cancellationToken);
                    locations.Add(new Location { File = location.SourceTree.FilePath, Line = lineSpan.StartLinePosition.Line + 1 });
                }
            }
        }
        
        return CreatePaginatedResult(locations, input.Page, input.PageSize);
    }
    
    private async Task<ToolResult> ListMembers(SymbolInput? input)
    {
        if (input == null) throw new JsonException("Input for ListMembers is null.");
        var cancellationToken = CancellationToken.None;

        var foundSymbols = await SymbolFinder.FindSourceDeclarationsAsync(_solution, name => name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase), cancellationToken);
        var symbol = foundSymbols.FirstOrDefault(s => s.Name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase)) ?? foundSymbols.FirstOrDefault();

        if (symbol is not INamedTypeSymbol typeSymbol)
        {
             return new ToolResult
             {
                IsError = true,
                Content = new List<McpContent> { new McpTextContent { Text = $"Error: Symbol '{input.Symbol}' is not a type or was not found." } }
             };
        }

        var methods = new List<string>();
        var properties = new List<string>();
        var fields = new List<string>();

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member.IsImplicitlyDeclared) continue;

            switch (member.Kind)
            {
                case SymbolKind.Method when member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary:
                    methods.Add(method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                    break;
                case SymbolKind.Property:
                    properties.Add(member.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                    break;
                case SymbolKind.Field:
                    fields.Add(member.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                    break;
            }
        }
        
        var result = new { methods, properties, fields };
        return new ToolResult { Content = new List<McpContent> { new McpTextContent { Text = JsonSerializer.Serialize(result) } } };
    }
    
    private ToolResult CreatePaginatedResult<T>(List<T> items, int page, int pageSize)
    {
        var totalItems = items.Count;
        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        var itemsForPage = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        
        var paginatedResult = new PaginatedOutput<T>
        {
            Items = itemsForPage,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
        
        return new ToolResult { Content = new List<McpContent> { new McpTextContent { Text = JsonSerializer.Serialize( paginatedResult) } } };
    }
}