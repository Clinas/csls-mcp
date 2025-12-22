# csls-mcp

`csls-mcp` (C# Language Service - Model Context Protocol) is a long-running .NET 8 Console Application that provides a C# Language Service for AI agents. It leverages the Roslyn compiler platform to offer semantic understanding of C# code within a given workspace, integrating seamlessly with the Gemini CLI via the Model Context Protocol (MCP).

The idea behind this MCP server is to provide agents with more efficient tools to use instead of searchText or readFile or tempt them to read more code efficiently rather than guessing what the code does or where things are, so features will be added or changed based on agent usage experience.

## 🎯 Objective

The primary goal of `csls-mcp` is to enable AI agents to semantically interact with C# codebases. Instead of relying on textual search, agents can use `csls-mcp` to:

-   **Resolve C# symbols:** Find classes, interfaces, methods, namespaces, properties, fields, etc.
-   **Find references:** Locate where a symbol is used.
-   **List members of types:** Enumerate methods, properties, and fields of a given class or interface.
-   **Obtain exact source code:** Retrieve the precise source text of a symbol's declaration.

TODO:

-   **FindDeclaration:** Locate where a symbol is declared.


This semantic understanding is crucial for tasks like intelligent code navigation, refactoring, bug fixing, and feature implementation by AI agents.

## 🧱 Architecture

-   **Type:** .NET 8 Console Application
-   **Execution:** Long-running process
-   **Communication:** Model Context Protocol (MCP) over Standard I/O (STDIO)
-   **Workspace Management:** Maintains the loaded C# solution/project in memory.
-   **Lifecycle:** Automatically started and stopped by the Gemini CLI.
-   **Roslyn:** Uses `Microsoft.CodeAnalysis` and `MSBuildWorkspace` for all semantic operations.


## 🚀 How to Compile

1.  **Navigate to the project directory:**
    ```bash
    cd D:\CSLS-MCP\csls-mcp
    ```
2.  **Restore NuGet packages and build the project:**
    ```bash
    dotnet build
    ```
    This will compile the application and place the executable in `bin\Debug\net8.0\`.

## 🤝 Integration with Gemini CLI

`csls-mcp` is designed to be integrated with Gemini CLI as an MCP server.

### ⚙️ Gemini CLI Configuration (`.gemini/settings.json`)

To enable Gemini CLI to use `csls-mcp`, add the following configuration to your `.gemini/settings.json` file in your project's root directory:

```json
{
  "mcpServers": {
    "csharp": {
      "command": "D:\\CSLS-MCP\\csls-mcp\\bin\\Debug\\net8.0\\csls-mcp.exe",
      "args": ["--workspace", "${workspaceFolder}"],
      "trust": true
    }
  }
}
```

-   **`csharp`**: This is the unique identifier (name) for this MCP server. You can choose any name you like.
-   **`command`**: The absolute path to the compiled `csls-mcp.exe` executable. Make sure to update this path if your build output directory is different.
-   **`args`**: Command-line arguments passed to the `csls-mcp` server.
    -   `--workspace ${workspaceFolder}`: This tells the server the path to the current workspace (your C# solution/project). `${workspaceFolder}` is a special variable that Gemini CLI resolves to the root of your current project.
-   **`trust`**: Must be `true` to allow the server to execute and interact with your workspace.

### ✨ Example Usage from Gemini CLI

Once configured, Gemini CLI will automatically start `csls-mcp` when needed and allow the agent to use its exposed tools. Here are examples of how an AI agent might interact with the `csharp` MCP server:

#### 1. `getToolDeclarations` (MCP Handshake)

This tool is used by the client (Gemini CLI) to discover the capabilities of the server.

**Request:**
```json
{
  "id": "1",
  "tool": "getToolDeclarations",
  "input": {}
}
```

**Response (example):**
```json
{
  "output": {
    "tools": [
      {
        "name": "resolveSymbol",
        "description": "Resolves a C# symbol (class, interface, method, etc.) and returns its kind, name, namespace, file, and line number.",
        "inputSchema": { "type": "object", "properties": { "symbol": { "type": "string" } }, "required": ["symbol"] }
      },
      {
        "name": "getSymbolSource",
        "description": "Retrieves the exact source code of a given C# symbol.",
        "inputSchema": { "type": "object", "properties": { "symbol": { "type": "string" } }, "required": ["symbol"] }
      },
      {
        "name": "findReferences",
        "description": "Finds all references to a given C# symbol within the loaded workspace.",
        "inputSchema": { "type": "object", "properties": { "symbol": { "type": "string" } }, "required": ["symbol"] }
      },
      {
        "name": "listMembers",
        "description": "Lists methods, properties, and fields of a specified C# type.",
        "inputSchema": { "type": "object", "properties": { "symbol": { "type": "string" } }, "required": ["symbol"] }
      }
    ]
  },
  "id": "1"
}
```

#### 2. `resolveSymbol`

**Request:**
```json
{
  "id": "2",
  "tool": "resolveSymbol",
  "input": {
    "symbol": "MyTestClass"
  }
}
```

**Response (example, assuming `MyTestClass` is in `dummy-project/Program.cs`):**
```json
{
  "output": {
    "kind": "Class",
    "name": "MyTestClass",
    "namespace": "dummy_project",
    "file": "D:\\CSLS-MCP\\dummy-project\\Program.cs",
    "line": 5
  },
  "id": "2"
}
```

#### 3. `getSymbolSource`

**Request:**
```json
{
  "id": "3",
  "tool": "getSymbolSource",
  "input": {
    "symbol": "MyTestMethod"
  }
}
```

**Response (example, showing source of `MyTestMethod`):**
```json
{
  "output": {
    "file": "D:\\CSLS-MCP\\dummy-project\\Program.cs",
    "source": "        public void MyTestMethod(string param)\n        {\n            Console.WriteLine($\"Parameter: {param}, Property: {MyProperty}, Field: {_myField}\");\n        }"
  },
  "id": "3"
}
```

#### 4. `findReferences`

**Request:**
```json
{
  "id": "4",
  "tool": "findReferences",
  "input": {
    "symbol": "MyTestClass"
  }
}
```

**Response (example, showing a reference in `Program.cs`):**
```json
{
  "output": [
    {
      "file": "D:\\CSLS-MCP\\dummy-project\\Program.cs",
      "line": 27
    }
  ],
  "id": "4"
}
```

#### 5. `listMembers`

**Request:**
```json
{
  "id": "5",
  "tool": "listMembers",
  "input": {
    "symbol": "MyTestClass"
  }
}
```

**Response (example):**
```json
{
  "output": {
    "methods": [
      "MyTestMethod(String param)"
    ],
    "properties": [
      "String MyProperty"
    ],
    "fields": [
      "Int32 _myField"
    ]
  },
  "id": "5"
}
```
