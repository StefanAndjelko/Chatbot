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

var chatBot = new Chat([localClient, everythingClient], ollamaModel, ollamaBaseUrl, 5);
await chatBot.RunAsync();