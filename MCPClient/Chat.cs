using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;

public class Chat
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;
    private readonly List<McpClient> _clients;
    private readonly ChatHistory _history = new();

    public Chat(Kernel kernel, List<McpClient> clients)
    {
        _kernel = kernel;
        _chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        _clients = clients;
    }

    public async Task RunAsync()
    {
        await RegisterMcpToolsAsync();
        Console.WriteLine("Type 'quit' to exit.\n");

        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        while (true)
        {
            Console.Write("You: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "quit")
                break;

            _history.AddUserMessage(input);

            Console.Write("\nAssistant: ");

            var fullContent = new StringBuilder();

            await foreach (var chunk in _chatCompletion.GetStreamingChatMessageContentsAsync(
                _history, settings, _kernel))
            {
                var text = chunk.Content ?? string.Empty;
                Console.Write(text);
                fullContent.Append(text);
            }

            Console.WriteLine("\n");

            _history.AddAssistantMessage(fullContent.ToString());
        }
    }

    private async Task RegisterMcpToolsAsync()
    {
        foreach (var client in _clients)
        {
            var mcpTools = await client.ListToolsAsync();
            var functions = new List<KernelFunction>();

            foreach (var tool in mcpTools)
            {
                var capturedTool = tool;
                var capturedClient = client;

                var function = KernelFunctionFactory.CreateFromMethod(
                    async (KernelArguments args) =>
                    {
                        var mcpArgs = args
                            .Where(a => a.Value != null)
                            .ToDictionary(a => a.Key, a => a.Value as object);

                        var argsJson = JsonSerializer.Serialize(mcpArgs);
                        Console.WriteLine($"\n  [Calling tool: {capturedTool.Name} | Args: {argsJson}]");

                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        var result = await capturedClient.CallToolAsync(capturedTool.Name, mcpArgs!);
                        var responseText = result.Content.First().ToString() ?? string.Empty;
                        sw.Stop();

                        Console.WriteLine($"  [Tool '{capturedTool.Name}' completed in {sw.ElapsedMilliseconds}ms | Response size: {responseText.Length} chars]");

                        return responseText;
                    },
                    functionName: capturedTool.Name,
                    description: capturedTool.Description,
                    parameters: ExtractParameters(capturedTool.JsonSchema)
                );

                functions.Add(function);
            }

            _kernel.Plugins.Add(
                KernelPluginFactory.CreateFromFunctions($"McpPlugin_{mcpTools.Count}", functions)
            );

            Console.WriteLine($"Registered {mcpTools.Count} tools from MCP server.");
        }
    }

    private static List<KernelParameterMetadata> ExtractParameters(JsonElement schema)
    {
        var parameters = new List<KernelParameterMetadata>();
        try
        {
            if (!schema.TryGetProperty("properties", out var props))
                return parameters;

            var required = new HashSet<string>();
            if (schema.TryGetProperty("required", out var req))
                foreach (var r in req.EnumerateArray())
                    required.Add(r.GetString()!);

            foreach (var prop in props.EnumerateObject())
            {
                var description = prop.Value.TryGetProperty("description", out var d)
                    ? d.GetString()
                    : null;

                parameters.Add(new KernelParameterMetadata(prop.Name)
                {
                    Description = description,
                    IsRequired = required.Contains(prop.Name)
                });
            }
        }
        catch { }

        return parameters;
    }
}