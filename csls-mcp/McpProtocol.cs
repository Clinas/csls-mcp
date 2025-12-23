using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace csls_mcp;

// Base classes for MCP messages
public class McpMessage
{
    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("jsonrpc")]
    public string JsonRPC { get; set; } = "2.0";
}

public class McpRequest : McpMessage
{
    [JsonPropertyName("method")]
    public required string Method { get; set; }

    [JsonPropertyName("params")]
    public JsonNode? Params { get; set; }
}

// New response structure
public class JsonRpcResultResponse<T> : McpMessage
{
    [JsonPropertyName("result")]
    public T Result { get; set; }
}

public class JsonRpcErrorResponse : McpMessage
{
    [JsonPropertyName("error")]
    public McpError Error { get; set; }
}

public class ToolResult
{
    [JsonPropertyName("content")]
    public List<McpContent> Content { get; set; }

    [JsonPropertyName("isError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsError { get; set; } = false;
}


// Polymorphic content items
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(McpTextContent), typeDiscriminator: "text")]
public abstract class McpContent
{
}

public class McpTextContent : McpContent
{
    [JsonPropertyName("text")]
    public required string Text { get; set; }
}

public class McpCodeContent : McpContent
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "csharp";

    [JsonPropertyName("code")]
    public required string Code { get; set; }
}

public class McpJsonContent : McpContent
{
    [JsonPropertyName("json")]
    public required object Json { get; set; }
}


public class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}

// Initialization and capabilities (unchanged for now)
public class InitializeParams
{
    [JsonPropertyName("protocolVersion")]
    public string? ProtocolVersion { get; set; }

    [JsonPropertyName("capabilities")]
    public ClientCapabilities? Capabilities { get; set; }

    [JsonPropertyName("clientInfo")]
    public ClientInfo? ClientInfo { get; set; }
}

public class ClientCapabilities
{
    [JsonPropertyName("roots")]
    public RootCapabilities? Roots { get; set; }
}

public class ClientInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

public class InitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "1.0";

    [JsonPropertyName("serverInfo")]
    public required ServerInfo ServerInfo { get; set; }

    [JsonPropertyName("capabilities")]
    public required ServerCapabilities Capabilities { get; set; }
}

public class ServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "csls-mcp";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";
}

public class ServerCapabilities
{
    [JsonPropertyName("roots")]
    public RootCapabilities Roots { get; set; } = new();

    [JsonPropertyName("tools")]
    public required Dictionary<string, ToolDeclaration> Tools { get; set; }
}

public class RootCapabilities
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; } = false;
}

public class ToolDeclaration
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("inputSchema")]
    public required JsonObject InputSchema { get; set; }
}

public class GetToolDeclarationsOutput
{
    [JsonPropertyName("tools")]
    public List<ToolDeclaration> Tools { get; set; } 
}

// Common input and data structures
public class SymbolInput
{
    [JsonPropertyName("symbol")]
    public required string Symbol { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 10;
}

public class PaginatedOutput<T>
{
    [JsonPropertyName("items")]
    public required List<T> Items { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }
}

public class Location
{
    [JsonPropertyName("file")]
    public required string File { get; set; }

    [JsonPropertyName("line")]
    public int Line { get; set; }
}

public class ToolCallParams
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("arguments")]
    public JsonNode? Arguments { get; set; }
}