using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Client;

public class Chat
{
    private readonly McpClient _mcpClient;
    private readonly string _model;
    private readonly string _baseUrl;
    private static readonly HttpClient _http = new();

    private readonly List<Dictionary<string, object>> _conversation = new();

    private int _maxToolCalls = 0;

    public Chat(McpClient mcpClient, string model, string baseUrl, int maxToolCalls)
    {
        _mcpClient = mcpClient;
        _model = model;
        _baseUrl = baseUrl;
        _maxToolCalls = maxToolCalls;
    }

    public async Task RunAsync()
    {
        var mcpTools = await _mcpClient.ListToolsAsync();
        var tools = mcpTools.Select(tool => (object)new
        {
            type = "function",
            function = new { name = tool.Name, description = tool.Description, parameters = tool.JsonSchema}
        }).ToList();

        Console.WriteLine($"Loaded {mcpTools.Count} tools from MCP server.");
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

            var reply = await ProcessQuery(tools);

            Console.WriteLine($"\nAssistant: {reply}\n");
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
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody);
            var response = await _http.PostAsync(
                $"{_baseUrl}/api/chat",
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var message = doc.RootElement.GetProperty("message");

            if (message.TryGetProperty("tool_calls", out var toolCalls)
                && toolCalls.GetArrayLength() > 0)
            {
                if (toolCounter + toolCalls.GetArrayLength() > _maxToolCalls) {
                    return $"Couldn't retrieve answer, too many tool calls. (Max tool calls: {_maxToolCalls}";
                }
                _conversation.Add(
                    JsonSerializer.Deserialize<Dictionary<string, object>>(message.GetRawText())!
                );

                foreach (var toolCall in toolCalls.EnumerateArray())
                {
                    var fnName = toolCall.GetProperty("function").GetProperty("name").GetString()!;
                    var fnArgs = toolCall.GetProperty("function").GetProperty("arguments");

                    Console.WriteLine($"  [Calling tool: {fnName} with arguments: {fnArgs.GetRawText()}]");

                    var mcpArgs = JsonSerializer.Deserialize<Dictionary<string, object?>>(fnArgs)!;

                    var toolResult = await _mcpClient.CallToolAsync(fnName, mcpArgs);
                    var resultText = toolResult.Content.First().ToString();

                    _conversation.Add(new Dictionary<string, object>
                    {
                        ["role"] = "tool",
                        ["content"] = resultText
                    });
                }

                continue;
            }

            var content = message.GetProperty("content").GetString()!;

            _conversation.Add(new Dictionary<string, object>
            {
                ["role"] = "assistant",
                ["content"] = content
            });

            return content;
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