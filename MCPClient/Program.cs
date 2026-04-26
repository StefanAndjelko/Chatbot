using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using System;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .Build();

var serverProjectPath = config["McpServer:ProjectPath"];
var ollamaBaseUrl = config["Ollama:BaseUrl"]!;
var ollamaModel = config["Ollama:Model"]!;

await using var mcpClient = await McpClient.CreateAsync(
    new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "McpServer",
        Command = "dotnet",
        Arguments = ["run", "--project", serverProjectPath, "--no-launch-profile"],
        WorkingDirectory = serverProjectPath,
    })
);

var chatBot = new Chat(mcpClient, ollamaModel, ollamaBaseUrl, 5);
await chatBot.RunAsync();