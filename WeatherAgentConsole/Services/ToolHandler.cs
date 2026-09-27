using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WeatherAgentConsole.Models;

namespace WeatherAgentConsole.Services;

public class ToolHandler
{
    private readonly GeocodingService _geocoding;
    private readonly WeatherService _weather;
    private readonly ILogger<ToolHandler> _logger;

    public ToolHandler(GeocodingService geocoding, WeatherService weather, ILogger<ToolHandler> logger)
    {
        _geocoding = geocoding;
        _weather = weather;
        _logger = logger;
    }

    public async Task<(ToolResult? result, string? error)> HandleToolCallAsync(ToolCall call, CancellationToken cancellationToken)
    {
        if (call.Tool != "get_weather")
        {
            return (null, $"Unknown tool '{call.Tool}'");
        }

        if (call.Arguments is not JsonElement argsEl)
        {
            return (null, "Missing arguments object");
        }

        try
        {
            var city = argsEl.TryGetProperty("city", out var cityEl) ? cityEl.GetString() : null;
            var country = argsEl.TryGetProperty("country", out var countryEl) ? countryEl.GetString() : null;

            if (string.IsNullOrWhiteSpace(city))
            {
                return (null, "city is required");
            }

            var geocode = await _geocoding.GeocodeAsync(city, country, cancellationToken);
            if (geocode == null)
            {
                return (null, $"Could not geocode location '{city}{(string.IsNullOrWhiteSpace(country) ? "" : $", {country}")}'");
            }

            var (lat, lon, display) = geocode.Value;
            var weather = await _weather.GetCurrentWeatherAsync(lat, lon, display, cancellationToken);

            if (!weather.IsSuccess)
            {
                return (null, weather.ErrorMessage ?? "Weather fetch failed");
            }

            var toolResult = new ToolResult
            {
                Tool = "get_weather",
                Result = new
                {
                    location = weather.Location,
                    temperature_c = weather.TemperatureCelsius,
                    wind_kph = weather.WindSpeedKph,
                    conditions = weather.Conditions,
                    observation_time = weather.ObservationTime
                }
            };

            return (toolResult, null);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            _logger.LogError(ex, "Tool handling failed");
            return (null, ex.Message);
        }
    }
}