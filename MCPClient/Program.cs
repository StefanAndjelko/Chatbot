using System;
using Anthropic.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ModelContextProtocol.Client;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .Build();

var provider = config["Llm:Provider"]!.ToLower();
var model = config["Llm:Model"]!;
var apiKey = config["Llm:ApiKey"]!;

var serverProjectPath = config["McpServer:ProjectPath"]!;

await using var localClient = await McpClient.CreateAsync(
    new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "McpServer",
        Command = "dotnet",
        Arguments = ["run", "--project", serverProjectPath, "--no-launch-profile"],
        WorkingDirectory = serverProjectPath,
    })
);

await using var everythingClient = await McpClient.CreateAsync(
    new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "McpServerEverything",
        Command = config["McpServerEverything:Command"]!,
        Arguments = config["McpServerEverything:Arguments"]!.Split(' '),
    })
);

var builder = Kernel.CreateBuilder();

switch (provider)
{
    case "openai":
        builder.AddOpenAIChatCompletion(model, apiKey);
        break;

    case "claude":
        var skChatService = new ChatClientBuilder(new AnthropicClient(apiKey).Messages)
            .UseFunctionInvocation()
            .Build()
            .AsChatCompletionService();
        builder.Services.AddSingleton<IChatCompletionService>(skChatService);
        break;

    case "ollama":
        builder.AddOllamaChatCompletion(model, new Uri(config["Ollama:BaseUrl"]!));
        break;

    default:
        throw new Exception($"Unknown provider '{provider}'. Use 'openai', 'claude', or 'ollama'.");
}

var kernel = builder.Build();

Console.WriteLine($"Provider: {provider} | Model: {model}\n");

var chat = new Chat(kernel, [localClient, everythingClient]);
await chat.RunAsync();