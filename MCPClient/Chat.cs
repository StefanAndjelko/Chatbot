using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Client;

public class Chat
{
    private readonly List<McpClient> _clients;
    private readonly string _model;
    private readonly string _baseUrl;
    private static readonly HttpClient _http = new();

    private readonly List<Dictionary<string, object>> _conversation = new();
    private readonly Dictionary<string, McpClient> _toolOwners = new();

    private int _maxToolCalls = 0;

    public Chat(List<McpClient> clients, string model, string baseUrl, int maxToolCalls)
    {
        _clients = clients;
        _model = model;
        _baseUrl = baseUrl;
        _maxToolCalls = maxToolCalls;
    }

    public async Task RunAsync()
    {
        List<object> tools = new List<object>();

        foreach (McpClient mcpClient in _clients)
        {
            var mcpTools = await mcpClient.ListToolsAsync();
            foreach (var tool in mcpTools)
            {
                _toolOwners.Add(tool.Name, mcpClient);
                tools.Add(new
                {
                    type = "function",
                    function = new
                    {
                        name = tool.Name,
                        description = tool.Description,
                        parameters = tool.JsonSchema
                    }
                });
            }
        }

        Console.WriteLine($"Loaded {tools.Count} tools from MCP servers.");
        Console.WriteLine($"Using model: {_model}");
        Console.WriteLine("Type 'quit' to exit.\n");

        while (true)
        {
            Console.Write("You: ");
            var userInput = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(userInput) || userInput.ToLower() == "quit")
                break;

            _conversation.Add(new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = userInput
            });

            await ProcessQuery(tools);
        }
    }

    private async Task<string> ProcessQuery(List<object> tools)
    {
        int toolCounter = 0;

        while (true)
        {
            var requestBody = new
            {
                model = _model,
                messages = _conversation,
                tools = tools,
                stream = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var response = await _http.PostAsync(
                $"{_baseUrl}/api/chat",
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            var fullContent = new StringBuilder();
            var toolCalls = new List<JsonElement>();
            var rawMessage = "";

            Console.Write("\nAssistant: ");

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var message = root.GetProperty("message");
                var isDone = root.TryGetProperty("done", out var done) && done.GetBoolean();

                if (message.TryGetProperty("tool_calls", out var chunkToolCalls)
                    && chunkToolCalls.GetArrayLength() > 0)
                {
                    rawMessage = message.GetRawText();
                    foreach (var tc in chunkToolCalls.EnumerateArray())
                        toolCalls.Add(tc.Clone());
                }

                if (message.TryGetProperty("content", out var contentChunk)
                    && contentChunk.GetString() is { Length: > 0 } fragment)
                {
                    Console.Write(fragment);
                    Console.Out.Flush();
                    await Task.Delay(25); // Artificial delay to create GPT-like effect
                    fullContent.Append(fragment);
                }

                if (isDone) break;
            }

            Console.WriteLine();

            if (toolCalls.Count > 0)
            {
                if (toolCounter + toolCalls.Count > _maxToolCalls)
                    return $"Too many tool calls (max: {_maxToolCalls})";

                _conversation.Add(
                    JsonSerializer.Deserialize<Dictionary<string, object>>(rawMessage)!
                );

                foreach (var toolCall in toolCalls)
                {
                    var fnName = toolCall.GetProperty("function").GetProperty("name").GetString()!;
                    var fnArgs = toolCall.GetProperty("function").GetProperty("arguments");

                    Console.WriteLine($"  [Calling tool: {fnName}, with arguments: {fnArgs}]");

                    var mcpArgs = JsonSerializer.Deserialize<Dictionary<string, object?>>(fnArgs)!;
                    var owningClient = _toolOwners[fnName];
                    var toolResult = await owningClient.CallToolAsync(fnName, mcpArgs);
                    var resultText = toolResult.Content.First().ToString()!;

                    _conversation.Add(new Dictionary<string, object>
                    {
                        ["role"] = "tool",
                        ["content"] = resultText
                    });

                    toolCounter++;
                }

                continue;
            }

            var finalContent = fullContent.ToString();
            _conversation.Add(new Dictionary<string, object>
            {
                ["role"] = "assistant",
                ["content"] = finalContent
            });

            return finalContent;
        }
    }

    //private List<object> RetrieveTools(IList<McpClientTool> mcpTools)
    //{
    //    // Ollama expects tools in this specific shape.
    //    // Conveniently, MCP's InputSchema is already a JSON Schema object
    //    // so we can pass it straight through.
    //    return mcpTools.Select(tool => (object)new
    //    {
    //        type = "function",
    //        function = new
    //        {
    //            name = tool.Name,
    //            description = tool.Description,
    //            parameters = tool.InputSchema
    //        }
    //    }).ToList();
    //}
}