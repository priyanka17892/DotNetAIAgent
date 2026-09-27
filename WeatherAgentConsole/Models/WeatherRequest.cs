namespace WeatherAgentConsole.Models;

public sealed class WeatherRequest
{
    public string City { get; init; } = "";
    public string? Country { get; init; }
}