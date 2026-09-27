namespace WeatherAgentConsole.Models;

public sealed class WeatherResponse
{
    public string Location { get; init; } = "";
    public double TemperatureCelsius { get; init; }
    public double WindSpeedKph { get; init; }
    public string Conditions { get; init; } = "";
    public DateTimeOffset ObservationTime { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
}