using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace csls_mcp
{
    public class McpMessage
    {
        [JsonPropertyName("id")]
        public object Id { get; set; }

        [JsonPropertyName("jsonrpc")]
        public string JsonRPC { get; set; } = "2.0";
    }

    public class McpRequest : McpMessage
    {
        [JsonPropertyName("method")]
        public string Method { get; set; }

        [JsonPropertyName("params")]
        public JsonNode Params { get; set; }
    }

    public class McpResponse : McpMessage
    {
        [JsonPropertyName("result")]
        public object Result { get; set; }

        [JsonPropertyName("error")]
        public McpError Error { get; set; }
    }

    public class McpError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("data")]
        public object Data { get; set; }
    }

    public class InitializeParams
    {
        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion { get; set; }

        [JsonPropertyName("capabilities")]
        public ClientCapabilities Capabilities { get; set; }

        [JsonPropertyName("clientInfo")]
        public ClientInfo ClientInfo { get; set; }
    }

    public class ClientCapabilities
    {
        [JsonPropertyName("roots")]
        public RootCapabilities Roots { get; set; }
    }

    public class ClientInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }
    }

    public class InitializeResult
    {
        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion { get; set; }

        [JsonPropertyName("serverInfo")]
        public ServerInfo ServerInfo { get; set; }

        [JsonPropertyName("capabilities")]
        public ServerCapabilities Capabilities { get; set; }
    }

    public class ServerInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }
    }

    public class ServerCapabilities
    {
        [JsonPropertyName("roots")]
        public RootCapabilities Roots { get; set; }

        [JsonPropertyName("tools")]
        public Dictionary<string, ToolDeclaration> Tools { get; set; }
    }

    public class RootCapabilities
    {
        [JsonPropertyName("listChanged")]
        public bool ListChanged { get; set; }
    }

    /// <summary>
    /// Input model for tools that take a single symbol name as input.
    /// </summary>
    public class SymbolInput
    {
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } // The name of the C# symbol.
    }

    /// <summary>
    /// Output model for the 'resolveSymbol' tool.
    /// </summary>
    public class ResolveSymbolOutput
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } // The kind of symbol (e.g., "Class", "Method", "Property").
        [JsonPropertyName("name")]
        public string Name { get; set; } // The name of the symbol.
        [JsonPropertyName("namespace")]
        public string Namespace { get; set; } // The containing namespace of the symbol.
        [JsonPropertyName("file")]
        public string File { get; set; } // The absolute path to the file where the symbol is declared.
        [JsonPropertyName("line")]
        public int Line { get; set; } // The 1-indexed line number of the symbol's declaration.
    }

    /// <summary>
    /// Output model for the 'getSymbolSource' tool.
    /// </summary>
    public class GetSymbolSourceOutput
    {
        [JsonPropertyName("file")]
        public string File { get; set; } // The absolute path to the file containing the source.
        [JsonPropertyName("source")]
        public string Source { get; set; } // The exact source code of the symbol's declaration.
    }

    /// <summary>
    /// Output model for a single reference found by the 'findReferences' tool.
    /// </summary>
    public class FindReferencesOutput
    {
        [JsonPropertyName("file")]
        public string File { get; set; } // The absolute path to the file where the reference is found.
        [JsonPropertyName("line")]
        public int Line { get; set; } // The 1-indexed line number of the reference.
    }

    /// <summary>
    /// Output model for the 'listMembers' tool.
    /// </summary>
    public class ListMembersOutput
    {
        [JsonPropertyName("methods")]
        public string[] Methods { get; set; } // Array of method signatures.
        [JsonPropertyName("properties")]
        public string[] Properties { get; set; } // Array of property signatures.
        [JsonPropertyName("fields")]
        public string[] Fields { get; set; } // Array of field signatures.
    }

    /// <summary>
    /// Represents the declaration of a single MCP tool, used for handshake and capability discovery.
    /// </summary>
    public class ToolDeclaration
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } // The programmatic name of the tool.
        [JsonPropertyName("description")]
        public string Description { get; set; } // A human-readable description of the tool.
        [JsonPropertyName("inputSchema")]
        public JsonObject InputSchema { get; set; } // A JSON schema describing the expected input for the tool.
    }

    /// <summary>
    /// Output model for the 'getToolDeclarations' tool.
    /// </summary>
        public class GetToolDeclarationsOutput
        {
            [JsonPropertyName("tools")]
            public List<ToolDeclaration> Tools { get; set; } // A list of all tool declarations.
        }
    
        public class ToolCallParams
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }
    
            [JsonPropertyName("arguments")]
            public JsonNode Arguments { get; set; }
        }
    }
    