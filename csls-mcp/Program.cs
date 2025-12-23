using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace csls_mcp
{
    /// <summary>
    /// Main entry point for the C# Language Service MCP server.
    /// This application loads a C# workspace (solution or project) using Roslyn
    /// and processes Model Context Protocol (MCP) requests via STDIO.
    /// </summary>
    class Program
    {
        // Roslyn workspace for loading and managing the C# solution/project.
        private static MSBuildWorkspace _workspace;
        // The currently loaded C# solution snapshot.
        private static Solution _currentSolution;

        // JsonSerializerOptions for consistent MCP message serialization/deserialization.
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true, // Allow case-insensitive property matching during deserialization
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // Omit null properties from JSON output
            WriteIndented = false // Keep JSON output compact for STDIO communication
        };

        private static void Log(string message)
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        }

        /// <summary>
        /// The main asynchronous entry point of the application.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        static async Task Main(string[] args)
        {
            var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);

            var logPath = Path.Combine(logDir, "stderr.log");

            var errorStream = new FileStream(
                logPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite);

            var errorWriter = new StreamWriter(errorStream)
            {
                AutoFlush = true
            };

            Console.SetError(errorWriter);

            Log("csls-mcp server started."); // Log server startup to stderr

            // Check for a --single-run argument to enable testing mode.
            // In single-run mode, the server processes one request and then exits.
            bool singleRun = args.Contains("--single-run", StringComparer.OrdinalIgnoreCase);

            // Parse the --workspace argument to get the path to the C# solution/project.
            string workspacePath = Directory.GetCurrentDirectory();

            // Load the C# workspace using Roslyn.
            await LoadWorkspaceAsync(workspacePath);

            // Check if the workspace was loaded successfully.
            if (_currentSolution == null)
            {
                Log($"Error: Could not load solution or project from {workspacePath}");
                Environment.Exit(1); // Exit if workspace loading failed
            }

            Log($"Successfully loaded workspace: {_currentSolution.FilePath}"); // Log successful workspace load

            // Initialize the McpHandler with the loaded solution and JSON serialization options.
            var mcpHandler = new McpHandler(_currentSolution, _jsonSerializerOptions);

            // Start the main STDIO loop to process incoming MCP requests.
            await ProcessStdioMessagesAsync(mcpHandler, singleRun);
        }

        /// <summary>
        /// Loads a C# solution or project into the MSBuildWorkspace.
        /// Handles both .sln and .csproj files, and directories containing them.
        /// </summary>
        /// <param name="workspacePath">The file or directory path to the C# workspace.</param>
        private static async Task LoadWorkspaceAsync(string workspacePath)
        {
            _workspace = MSBuildWorkspace.Create(); // Create a new MSBuildWorkspace instance
            _workspace.LoadMetadataForReferencedProjects = true; // Ensure project references are loaded

            // Register an event handler for workspace loading failures to log diagnostic messages.
            // Note: WorkspaceFailed is obsolete; for production, consider RegisterWorkspaceFailedHandler.
            _workspace.WorkspaceFailed += (sender, e) =>
            {
                Log($"MSBuildWorkspace failed: {e.Diagnostic.Message}");
            };

            try
            {
                if (File.Exists(workspacePath))
                {
                    // Load a specific solution file (.sln)
                    if (workspacePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                    {
                        Log($"Loading solution: {workspacePath}");
                        _currentSolution = await _workspace.OpenSolutionAsync(workspacePath, null, CancellationToken.None);
                    }
                    // Load a specific project file (.csproj)
                    else if (workspacePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                    {
                        Log($"Loading project: {workspacePath}");
                        var project = await _workspace.OpenProjectAsync(workspacePath, null, CancellationToken.None);
                        _currentSolution = project.Solution; // Get the solution containing the project
                    }
                    else
                    {
                        Log($"Unsupported file type for workspace: {workspacePath}. Expected .sln or .csproj");
                    }
                }
                else if (Directory.Exists(workspacePath))
                {
                    Log($"Searching for .sln or .csproj in directory: {workspacePath}");
                    // Search for solution files recursively within the directory
                    var solutionFiles = Directory.EnumerateFiles(workspacePath, "*.sln", SearchOption.AllDirectories).ToList();
                    // Search for project files recursively within the directory
                    var projectFiles = Directory.EnumerateFiles(workspacePath, "*.csproj", SearchOption.AllDirectories).ToList();

                    if (solutionFiles.Any())
                    {
                        // If multiple solutions are found, load the first one and warn.
                        if (solutionFiles.Count > 1)
                        {
                            Log($"Warning: Multiple solution files found in {workspacePath}. Loading the first one: {solutionFiles.First()}");
                        }
                        Log($"Loading solution: {solutionFiles.First()}");
                        _currentSolution = await _workspace.OpenSolutionAsync(solutionFiles.First(), null, CancellationToken.None);
                    }
                    else if (projectFiles.Any())
                    {
                        // If multiple projects are found, load the first one and warn.
                         if (projectFiles.Count > 1)
                        {
                            Log($"Warning: Multiple project files found in {workspacePath}. Loading the first one: {projectFiles.First()}");
                        }
                        Log($"Loading project: {projectFiles.First()}");
                        var project = await _workspace.OpenProjectAsync(projectFiles.First(), null, CancellationToken.None);
                        _currentSolution = project.Solution; // Get the solution containing the project
                    }
                    else
                    {
                        Log($"No .sln or .csproj found in {workspacePath}");
                    }
                }
                else
                {
                    Log($"Workspace path does not exist: {workspacePath}");
                }
            }
            catch (Exception ex)
            {
                // Log any exceptions that occur during workspace loading.
                Log($"Exception while loading workspace: {ex.Message}");
                Log(ex.ToString());
            }
        }

        /// <summary>
        /// Processes incoming MCP messages from standard input (stdin) and sends responses to standard output (stdout).
        /// </summary>
        /// <param name="mcpHandler">The handler responsible for dispatching and executing MCP tools.</param>
        /// <param name="singleRun">If true, the loop exits after processing the first request.</param>
        private static async Task ProcessStdioMessagesAsync(McpHandler mcpHandler, bool singleRun)
        {
            Log("Starting STDIO message processing loop...");
            string line;
            // Continuously read lines from stdin until the stream is closed.
            while ((line = await Console.In.ReadLineAsync()) != null)
            {
                McpMessage? response = null;
                McpRequest? request = null;

                try
                {
                    Log($"Received raw: {line}"); // Log raw incoming message
                    // Attempt to deserialize the incoming JSON line into an McpRequest object.
                    request = JsonSerializer.Deserialize<McpRequest>(line, _jsonSerializerOptions);
                    if (request == null)
                    {
                        throw new JsonException("Deserialized request is null."); // Invalid request format
                    }

                    // Hand off the request to the McpHandler for processing.
                    response = await mcpHandler.HandleRequest(request);
                }
                catch (JsonException ex)
                {
                    // Handle JSON deserialization errors for the request.
                    Log($"JSON deserialization error: {ex.Message}");
                    response = new JsonRpcErrorResponse
                    {
                        Id = TryGetIdFromRaw(line), // Attempt to get the ID for the response
                        Error = new McpError { Code = -32700, Message = "Parse Error", Data = ex.Message }
                    };
                }
                catch (Exception ex)
                {
                    // Handle any other unexpected errors during request processing.
                    Log($"Unexpected error processing request: {ex.Message}");
                    response = new JsonRpcErrorResponse
                    {
                        Id = request?.Id,
                        Error = new McpError { Code = -32603, Message = "Internal Server Error", Data = ex.ToString() }
                    };
                }

                if (response != null)
                {
                    // Serialize the response object to JSON using its actual runtime type and send it to stdout.
                    string jsonResponse = JsonSerializer.Serialize(response, response.GetType(), _jsonSerializerOptions);
                    await Console.Out.WriteLineAsync(jsonResponse);
                    await Console.Out.FlushAsync(); // Ensure the output is immediately written
                    Log($"Sent: {jsonResponse}"); // Log sent response
                }

                // If in single-run mode, break the loop and exit after the first request.
                if (singleRun)
                {
                    Log("Single run mode enabled. Exiting after first request.");
                    break;
                }
            }
            Log("STDIO input stream closed. Exiting."); // Log when stdin stream closes
        }

        private static object? TryGetIdFromRaw(string rawJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                if (doc.RootElement.TryGetProperty("id", out var idElement))
                {
                    if (idElement.ValueKind == JsonValueKind.String)
                        return idElement.GetString();
                    if (idElement.ValueKind == JsonValueKind.Number)
                        return idElement.GetInt64();
                    return idElement.ToString();
                }
            }
            catch
            {
                // Ignore if parsing fails, we just can't get an ID.
            }
            return null;
        }
    }
}
