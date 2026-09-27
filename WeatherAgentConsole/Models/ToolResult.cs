namespace WeatherAgentConsole.Models;

public sealed class ToolResult
{
    public string Tool { get; init; } = "";
    public object Result { get; init; } = new { };
}