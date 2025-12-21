using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace csls_mcp
{
    /// <summary>
    /// Handles incoming Model Context Protocol (MCP) requests and dispatches them to the appropriate Roslyn-based tools.
    /// </summary>
    public class McpHandler
    {
        private readonly Solution _solution; // The currently loaded Roslyn solution.
        private readonly JsonSerializerOptions _jsonSerializerOptions; // JSON serialization options for consistency.

        /// <summary>
        /// Initializes a new instance of the <see cref="McpHandler"/> class.
        /// </summary>
        /// <param name="solution">The Roslyn solution to perform operations on.</param>
        /// <param name="jsonSerializerOptions">JSON serialization options.</param>
        public McpHandler(Solution solution, JsonSerializerOptions jsonSerializerOptions)
        {
            _solution = solution ?? throw new ArgumentNullException(nameof(solution));
            _jsonSerializerOptions = jsonSerializerOptions;
        }

        /// <summary>
        /// Handles a generic MCP request by dispatching it to the specific tool implementation.
        /// </summary>
        /// <param name="request">The incoming MCP request.</param>
        /// <returns>An MCP response containing the tool's output or an error.</returns>
        public async Task<McpResponse> HandleRequest(McpRequest request)
        {
            var response = new McpResponse { Id = request.Id };

            try
            {
                switch (request.Method)
                {
                    case "initialize":
                        response.Result = Initialize(request.Params.Deserialize<InitializeParams>(_jsonSerializerOptions));
                        break;
                    case "notifications/initialized":
                        return null; // This is a notification, so we don't send a response.
                    case "tools/list":
                        response.Result = GetToolDeclarations();
                        break;
                    case "getToolDeclarations":
                        response.Result = GetToolDeclarations();
                        break;
                    case "resolveSymbol":
                        response.Result = await ResolveSymbol(request.Params.Deserialize<SymbolInput>(_jsonSerializerOptions));
                        break;
                    case "getSymbolSource":
                        response.Result = await GetSymbolSource(request.Params.Deserialize<SymbolInput>(_jsonSerializerOptions));
                        break;
                    case "findReferences":
                        response.Result = await FindReferences(request.Params.Deserialize<SymbolInput>(_jsonSerializerOptions));
                        break;
                    case "listMembers":
                        response.Result = await ListMembers(request.Params.Deserialize<SymbolInput>(_jsonSerializerOptions));
                        break;
                    case "tools/call":
                        var toolCallParams = request.Params.Deserialize<ToolCallParams>(_jsonSerializerOptions);
                        switch (toolCallParams.Name)
                        {
                            case "resolveSymbol":
                                response.Result = await ResolveSymbol(toolCallParams.Arguments.Deserialize<SymbolInput>(_jsonSerializerOptions));
                                break;
                            case "getSymbolSource":
                                response.Result = await GetSymbolSource(toolCallParams.Arguments.Deserialize<SymbolInput>(_jsonSerializerOptions));
                                break;
                            case "findReferences":
                                response.Result = await FindReferences(toolCallParams.Arguments.Deserialize<SymbolInput>(_jsonSerializerOptions));
                                break;
                            case "listMembers":
                                response.Result = await ListMembers(toolCallParams.Arguments.Deserialize<SymbolInput>(_jsonSerializerOptions));
                                break;
                            default:
                                response.Error = new McpError { Code = -32601, Message = "Method not found", Data = $"Unknown tool: {toolCallParams.Name}" };
                                break;
                        }
                        break;
                    default:
                        response.Error = new McpError { Code = -32601, Message = "Method not found", Data = $"Unknown method: {request.Method}" };
                        break;
                }
            }
            catch (JsonException ex)
            {
                response.Error = new McpError { Code = -32700, Message = "Invalid JSON input for tool", Data = ex.Message };
            }
            catch (Exception ex)
            {
                response.Error = new McpError { Code = -32000, Message = "Server error during tool execution", Data = ex.ToString() };
            }

            return response;
        }

        private InitializeResult Initialize(InitializeParams args)
        {
            var tools = GetToolDeclarations().Tools;
            var toolDictionary = tools.ToDictionary(t => t.Name, t => t);

            return new InitializeResult
            {
                ProtocolVersion = "2025-11-25",
                ServerInfo = new ServerInfo
                {
                    Name = "csls-mcp",
                    Version = "0.0.1"
                },
                Capabilities = new ServerCapabilities
                {
                    Roots = new RootCapabilities
                    {
                        ListChanged = true
                    },
                    Tools = toolDictionary
                }
            };
        }

        /// <summary>
        /// Returns a list of all tools exposed by this MCP server, including their names, descriptions, and input schemas.
        /// This is part of the MCP handshake mechanism.
        /// </summary>
        /// <returns>An object containing a list of <see cref="ToolDeclaration"/>.</returns>
        private GetToolDeclarationsOutput GetToolDeclarations()
        {
            return new GetToolDeclarationsOutput
            {
                Tools = new List<ToolDeclaration>
                {
                    new ToolDeclaration
                    {
                        Name = "resolveSymbol",
                        Description = "Resolves a C# symbol (class, interface, method, etc.) and returns its kind, name, namespace, file, and line number.",
                        InputSchema = JsonNode.Parse("{\"type\": \"object\", \"properties\": {\"symbol\": {\"type\": \"string\"}}, \"required\": [\"symbol\"]}")?.AsObject()
                    },
                    new ToolDeclaration
                    {
                        Name = "getSymbolSource",
                        Description = "Retrieves the exact source code of a given C# symbol.",
                        InputSchema = JsonNode.Parse("{\"type\": \"object\", \"properties\": {\"symbol\": {\"type\": \"string\"}}, \"required\": [\"symbol\"]}")?.AsObject()
                    },
                    new ToolDeclaration
                    {
                        Name = "findReferences",
                        Description = "Finds all references to a given C# symbol within the loaded workspace.",
                        InputSchema = JsonNode.Parse("{\"type\": \"object\", \"properties\": {\"symbol\": {\"type\": \"string\"}}, \"required\": [\"symbol\"]}")?.AsObject()
                    },
                    new ToolDeclaration
                    {
                        Name = "listMembers",
                        Description = "Lists methods, properties, and fields of a specified C# type.",
                        InputSchema = JsonNode.Parse("{\"type\": \"object\", \"properties\": {\"symbol\": {\"type\": \"string\"}}, \"required\": [\"symbol\"]}")?.AsObject()
                    }
                }
            };
        }

        /// <summary>
        /// Resolves a C# symbol by its name within the loaded solution.
        /// </summary>
        /// <param name="input">The input containing the symbol name.</param>
        /// <returns>Details about the resolved symbol, or a "NotFound" placeholder.</returns>
        private async Task<ResolveSymbolOutput> ResolveSymbol(SymbolInput input)
        {
            var searchCancellationToken = CancellationToken.None; // A cancellation token, can be used to cancel long-running operations.

            // Use Roslyn's SymbolFinder to find source declarations matching the symbol name across the entire solution.
            // The lambda predicate ensures a case-insensitive match.
            var foundSymbols = await SymbolFinder.FindSourceDeclarationsAsync(
                _solution, (name) => name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase), searchCancellationToken);

            // Prioritize an exact match (case-insensitive) first.
            var exactMatch = foundSymbols.FirstOrDefault(s => s.Name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase));
            ISymbol symbolToResolve = exactMatch ?? foundSymbols.FirstOrDefault(); // If no exact match, take the first found symbol.

            if (symbolToResolve != null)
            {
                // Get the first source location where the symbol is declared.
                var location = symbolToResolve.Locations.FirstOrDefault(loc => loc.IsInSource);
                if (location != null && location.SourceTree != null)
                {
                    // Get the line span for the symbol's location.
                    var lineSpan = location.SourceTree.GetLineSpan(location.SourceSpan, searchCancellationToken);

                    // Return the resolved symbol's details.
                    return new ResolveSymbolOutput
                    {
                        Kind = symbolToResolve.Kind.ToString(), // e.g., "Class", "Method", "Property"
                        Name = symbolToResolve.Name,
                        Namespace = symbolToResolve.ContainingNamespace?.ToDisplayString() ?? string.Empty, // Display full namespace name
                        File = location.SourceTree.FilePath, // Absolute path to the source file
                        Line = lineSpan.StartLinePosition.Line + 1 // Convert 0-indexed Roslyn line to 1-indexed for display
                    };
                }
            }

            // Return a "NotFound" response if the symbol or its source location could not be determined.
            return new ResolveSymbolOutput
            {
                Kind = "NotFound",
                Name = input.Symbol,
                Namespace = string.Empty,
                File = string.Empty,
                Line = 0
            };
        }

        /// <summary>
        /// Retrieves the exact source code of a specified C# symbol.
        /// </summary>
        /// <param name="input">The input containing the symbol name.</param>
        /// <returns>The file path and the source code snippet of the symbol's declaration.</returns>
        private async Task<GetSymbolSourceOutput> GetSymbolSource(SymbolInput input)
        {
            var searchCancellationToken = CancellationToken.None;

            // Find the symbol declaration using SymbolFinder.
            var foundSymbols = await SymbolFinder.FindSourceDeclarationsAsync(
                _solution, (name) => name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase), searchCancellationToken);

            var exactMatch = foundSymbols.FirstOrDefault(s => s.Name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase));
            ISymbol symbolToResolve = exactMatch ?? foundSymbols.FirstOrDefault();

            if (symbolToResolve != null)
            {
                // Get the first declaring syntax reference for the symbol.
                var declaringReference = symbolToResolve.DeclaringSyntaxReferences.FirstOrDefault();
                if (declaringReference != null)
                {
                    // Retrieve the SyntaxNode corresponding to the declaration.
                    var syntaxNode = await declaringReference.GetSyntaxAsync(searchCancellationToken);
                    // Return the file path and the full text of the syntax node.
                    return new GetSymbolSourceOutput
                    {
                        File = declaringReference.SyntaxTree.FilePath,
                        Source = syntaxNode.ToFullString()
                    };
                }
            }

            // Return a "not found" response if the source could not be retrieved.
            return new GetSymbolSourceOutput
            {
                File = string.Empty,
                Source = "// Source for '" + input.Symbol + "' not found or could not be retrieved."
            };
        }

        /// <summary>
        /// Finds all references to a specified C# symbol within the loaded solution.
        /// </summary>
        /// <param name="input">The input containing the symbol name.</param>
        /// <returns>A list of locations (file path and line number) where the symbol is referenced.</returns>
        private async Task<List<FindReferencesOutput>> FindReferences(SymbolInput input)
        {
            var searchCancellationToken = CancellationToken.None;
            var references = new List<FindReferencesOutput>();

            // Find the symbol declaration using SymbolFinder.
            var foundSymbols = await SymbolFinder.FindSourceDeclarationsAsync(
                _solution, (name) => name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase), searchCancellationToken);

            var exactMatch = foundSymbols.FirstOrDefault(s => s.Name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase));
            ISymbol symbolToFind = exactMatch ?? foundSymbols.FirstOrDefault();

            if (symbolToFind != null)
            {
                // Find all references to the identified symbol across the entire solution.
                var symbolReferences = await SymbolFinder.FindReferencesAsync(symbolToFind, _solution, searchCancellationToken);

                // Iterate through each reference and extract its location.
                foreach (var reference in symbolReferences)
                {
                    foreach (var location in reference.Locations)
                    {
                        // Get the line position for the start of the reference's source span.
                        var lineSpan = location.Document.GetTextAsync(searchCancellationToken).Result.Lines.GetLinePosition(location.Location.SourceSpan.Start);
                        references.Add(new FindReferencesOutput
                        {
                            File = location.Document.FilePath,
                            Line = lineSpan.Line + 1 // Convert 0-indexed Roslyn line to 1-indexed.
                        });
                    }
                }
            }
            return references;
        }

        /// <summary>
        /// Lists the methods, properties, and fields of a specified C# type.
        /// </summary>
        /// <param name="input">The input containing the type name.</param>
        /// <returns>An object containing arrays of method, property, and field names.</returns>
        private async Task<ListMembersOutput> ListMembers(SymbolInput input)
        {
            var searchCancellationToken = CancellationToken.None;
            var output = new ListMembersOutput
            {
                Methods = Array.Empty<string>(),
                Properties = Array.Empty<string>(),
                Fields = Array.Empty<string>()
            };

            // Find the symbol declaration for the input type.
            var foundSymbols = await SymbolFinder.FindSourceDeclarationsAsync(
                _solution, (name) => name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase), searchCancellationToken);

            var exactMatch = foundSymbols.FirstOrDefault(s => s.Name.Equals(input.Symbol, StringComparison.OrdinalIgnoreCase));
            ISymbol symbolToExamine = exactMatch ?? foundSymbols.FirstOrDefault();

            // If the symbol is a named type (class, struct, interface, enum), get its members.
            if (symbolToExamine is INamedTypeSymbol typeSymbol)
            {
                var methods = new List<string>();
                var properties = new List<string>();
                var fields = new List<string>();

                // Iterate through all members of the type.
                foreach (var member in typeSymbol.GetMembers())
                {
                    // Skip members that are implicitly declared (e.g., backing fields for properties).
                    if (member.IsImplicitlyDeclared) continue;

                    // Categorize members based on their SymbolKind.
                    switch (member.Kind)
                    {
                        case SymbolKind.Method:
                            // Filter out constructors, destructors, operators, and property/event accessors.
                            if (member is IMethodSymbol method &&
                                method.MethodKind != MethodKind.Constructor &&
                                method.MethodKind != MethodKind.Destructor &&
                                method.MethodKind != MethodKind.UserDefinedOperator &&
                                method.MethodKind != MethodKind.Conversion &&
                                method.MethodKind != MethodKind.PropertyGet &&
                                method.MethodKind != MethodKind.PropertySet &&
                                method.MethodKind != MethodKind.EventAdd &&
                                method.MethodKind != MethodKind.EventRemove)
                            {
                                methods.Add(method.ToDisplayString(new SymbolDisplayFormat(
                                    globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                                    typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
                                    propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
                                    genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                                    memberOptions: SymbolDisplayMemberOptions.IncludeParameters,
                                    delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
                                    extensionMethodStyle: SymbolDisplayExtensionMethodStyle.Default,
                                    parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName,
                                    miscellaneousOptions: SymbolDisplayMiscellaneousOptions.None
                                )));
                            }
                            break;
                        case SymbolKind.Property:
                            properties.Add(member.ToDisplayString(new SymbolDisplayFormat(
                                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
                                propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
                                memberOptions: SymbolDisplayMemberOptions.IncludeType
                            )));
                            break;
                        case SymbolKind.Field:
                            fields.Add(member.ToDisplayString(new SymbolDisplayFormat(
                                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
                                propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
                                memberOptions: SymbolDisplayMemberOptions.IncludeType
                            )));
                            break;
                    }
                }

                output.Methods = methods.ToArray();
                output.Properties = properties.ToArray();
                output.Fields = fields.ToArray();
            }

            return output;
        }
    }
}