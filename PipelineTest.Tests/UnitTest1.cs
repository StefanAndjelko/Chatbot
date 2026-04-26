using System;
using Microsoft.Extensions.Configuration;
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

    [Fact]
    public async Task SendMessage_ReturnsResponse()
    {
        var model = _config["Llm:Model"]!;
        var baseUrl = _config["Ollama:BaseUrl"]!;

        var kernel = Kernel.CreateBuilder()
            .AddOllamaChatCompletion(model, new Uri(baseUrl))
            .Build();

        var chat = new Chat(kernel, []);

        var response = await chat.SendMessageAsync("Say hello in one sentence.");

        Assert.False(string.IsNullOrWhiteSpace(response));
    }
}