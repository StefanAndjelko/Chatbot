using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

[McpServerToolType]
public class SearchTools
{
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "McpSearchServer/1.0" } }
    };

    [McpServerTool]
    [Description("Search the web using DuckDuckGo and return a summary of results.")]
    public static async Task<string> SearchWeb(
        [Description("The search query string")] string query,
        [Description("Max number of results to return (default 5)")] int maxResults = 5)
    {
        var url = $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(query)}" +
                  $"&format=json&no_html=1&skip_disambig=1";

        var json = await Http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var parts = new List<string>();

        if (root.TryGetProperty("AbstractText", out var abs) && abs.GetString() is { Length: > 0 } absText)
            parts.Add($"Summary: {absText}");

        if (root.TryGetProperty("RelatedTopics", out var topics))
        {
            int count = 0;
            foreach (var topic in topics.EnumerateArray())
            {
                if (count >= maxResults) break;
                if (topic.TryGetProperty("Text", out var text) &&
                    topic.TryGetProperty("FirstURL", out var link))
                {
                    parts.Add($"- {text.GetString()}\n  URL: {link.GetString()}");
                    count++;
                }
            }
        }

        return parts.Count > 0
            ? string.Join("\n\n", parts)
            : $"No results found for: {query}";
    }

    [McpServerTool]
    [Description("Get the current date and time.")]
    public static string GetCurrentDateTime()
    {
        return DateTime.Now.ToString("F");
    }

    [McpServerTool]
    [Description("Add two numbers together and return sum")]
    public static int AddNumbers(
        [Description("First number to add")] int a,
        [Description("Second number to add")] int b)
    {
        return a + b;
    }
}