using System;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WeatherAgentConsole.Models;

namespace WeatherAgentConsole.Services;

public class WeatherAgent
{
    private readonly AnthropicClient _anthropic;
    private readonly ToolHandler _toolHandler;
    private readonly ILogger<WeatherAgent> _logger;

    public WeatherAgent(AnthropicClient anthropic, ToolHandler toolHandler, ILogger<WeatherAgent> logger)
    {
        _anthropic = anthropic;
        _toolHandler = toolHandler;
        _logger = logger;
    }

    public async Task<string> HandleUserInputAsync(string userInput, CancellationToken cancellationToken)
    {
        var system = new StringBuilder();
        system.AppendLine("You are Claude, a helpful assistant. If the user asks for current weather, temperature, or whether it will rain for a specific city, you SHOULD call the tool 'get_weather'.");
        system.AppendLine();
        system.AppendLine("Tool schema:");
        system.AppendLine("Tool name: get_weather");
        system.AppendLine("Input JSON schema:");
        system.AppendLine("{");
        system.AppendLine("  \"city\": \"City name, required\",");
        system.AppendLine("  \"country\": \"Country name, optional\"");
        system.AppendLine("}");
        system.AppendLine();
        system.AppendLine("When you decide that calling the tool is necessary, respond with ONLY a single JSON object (no surrounding text) like:");
        system.AppendLine("{ \"tool\": \"get_weather\", \"arguments\": { \"city\": \"Berlin\", \"country\": \"Germany\" } }");
        system.AppendLine();
        system.AppendLine("If no tool is needed, respond with a normal natural-language answer and do not output JSON.");
        system.AppendLine();
        system.AppendLine("Be concise and ensure any JSON output is valid JSON (no trailing text).");

        var prompt = $"{system}\nUser: {userInput}\nAssistant:";

        var firstResponse = await _anthropic.SendConversationAsync(prompt, cancellationToken);
        _logger.LogDebug("First Claude response: {Resp}", firstResponse);

        var toolCall = TryExtractToolCall(firstResponse, out var parseError);
        if (toolCall == null)
        {
            return firstResponse.Trim();
        }

        var (toolResult, error) = await _toolHandler.HandleToolCallAsync(toolCall, cancellationToken);
        if (error != null)
        {
            var errorPrompt = $"Tool execution failed: {error}\nAssistant:";
            var errorResp = await _anthropic.SendConversationAsync(errorPrompt, cancellationToken);
            return errorResp.Trim();
        }

        var toolResultJson = JsonSerializer.Serialize(toolResult!.Result, new JsonSerializerOptions { WriteIndented = false });
        var followupPrompt = $"Tool result for {toolResult.Tool}:\n{toolResultJson}\nBased on the tool result, produce a short natural-language answer to the original user question: \"{userInput}\"";
        var finalResp = await _anthropic.SendConversationAsync(followupPrompt, cancellationToken);
        return finalResp.Trim();
    }

    private ToolCall? TryExtractToolCall(string text, out string? error)
    {
        error = null;
        var json = ExtractFirstJsonObject(text);
        if (json == null) return null;

        try
        {
            var toolCall = JsonSerializer.Deserialize<ToolCall>(json);
            return toolCall;
        }
        catch (JsonException je)
        {
            error = je.Message;
            _logger.LogWarning("Failed to parse tool JSON: {Msg}. Raw text: {Text}", je.Message, text);
            return null;
        }
    }

    private static string? ExtractFirstJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var trimmed = text.Trim();
        var fencePattern = new Regex(@"^```(?:json)?\s*(.*)\s*```$", RegexOptions.Singleline);
        var m = fencePattern.Match(trimmed);
        if (m.Success) trimmed = m.Groups[1].Value.Trim();

        int start = trimmed.IndexOf('{');
        if (start < 0) return null;
        int depth = 0;
        for (int i = start; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    var candidate = trimmed.Substring(start, i - start + 1);
                    return candidate;
                }
            }
        }

        return null;
    }
}