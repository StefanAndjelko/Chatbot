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
using OpenAI.Responses;

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

    public async Task<string> SendMessageAsync(string input)
    {
        _history.AddUserMessage(input);

        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var fullContent = new StringBuilder();

        await foreach (var chunk in _chatCompletion.GetStreamingChatMessageContentsAsync(
            _history, settings, _kernel))
        {
            fullContent.Append(chunk.Content ?? string.Empty);
        }

        var response = fullContent.ToString();
        _history.AddAssistantMessage(response);
        return response;
    }

    public async Task RunAsync()
    {
        await RegisterMcpToolsAsync();
        Console.WriteLine("Type 'quit' to exit.\n");

        while (true)
        {
            Console.Write("You: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "quit")
                break;

            Console.Write("\nAssistant: ");
            var response = await SendMessageAsync(input);
            Console.WriteLine(response + "\n");
        }
    }

    private async Task RegisterMcpToolsAsync()
    {
        int toolCount = 0;
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
            toolCount += mcpTools.Count;
            
        }

        Console.WriteLine($"Registered {toolCount} tools from MCP servers.");
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