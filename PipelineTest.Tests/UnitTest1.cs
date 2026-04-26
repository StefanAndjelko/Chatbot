using System;
using Anthropic.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

public class ChatIntegrationTests
{
    private readonly IConfiguration _config;

    public ChatIntegrationTests()
    {
        _config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .AddUserSecrets<ChatIntegrationTests>()
            .Build();
    }

    private Kernel BuildKernel()
    {
        var provider = _config["Llm:Provider"]!.ToLower();
        var model = _config["Llm:Model"]!;
        var apiKey = _config["Llm:ApiKey"];
        var builder = Kernel.CreateBuilder();

        switch (provider)
        {
            case "openai":
                builder.AddOpenAIChatCompletion(model, apiKey!);
                break;
            case "claude":
                var skChatService = new ChatClientBuilder(new AnthropicClient(apiKey!).Messages)
                    .UseFunctionInvocation()
                    .Build()
                    .AsChatCompletionService();
                builder.Services.AddSingleton<IChatCompletionService>(skChatService);
                break;
            case "ollama":
                builder.AddOllamaChatCompletion(model, new Uri(_config["Ollama:BaseUrl"]!));
                break;
            default:
                throw new Exception($"Unknown provider '{provider}'. Use 'openai', 'claude', or 'ollama'.");
        }

        return builder.Build();
    }

    [Fact]
    public async Task SendMessage_ReturnsResponse()
    {
        var kernel = BuildKernel();
        var chat = new Chat(kernel, []);

        var response = await chat.SendMessageAsync("Say hello in one sentence.");

        Assert.False(string.IsNullOrWhiteSpace(response));
    }

    [Fact]
    public async Task SendMessage_WithFakeClaudeKey_ReturnsUnauthorized()
    {
        var builder = Kernel.CreateBuilder();
        var skChatService = new ChatClientBuilder(new AnthropicClient("sk-fake-key").Messages)
            .UseFunctionInvocation()
            .Build()
            .AsChatCompletionService();
        builder.Services.AddSingleton<IChatCompletionService>(skChatService);
        var kernel = builder.Build();
        var chat = new Chat(kernel, []);

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => chat.SendMessageAsync("Say hello."));

        Assert.Contains("Anthropic rejected your authorization", exception.Message);
    }
}