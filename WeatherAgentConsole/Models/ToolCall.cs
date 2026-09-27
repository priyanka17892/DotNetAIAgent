using System.Text.Json;

namespace WeatherAgentConsole.Models;

public sealed class ToolCall
{
    public string Tool { get; init; } = "";
    public JsonElement? Arguments { get; init; }
}