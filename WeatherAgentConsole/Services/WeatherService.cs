using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WeatherAgentConsole.Models;

namespace WeatherAgentConsole.Services;

public class WeatherService
{
    private readonly HttpClient _http;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(IHttpClientFactory httpFactory, ILogger<WeatherService> logger)
    {
        _http = httpFactory.CreateClient("open-meteo");
        _logger = logger;
    }

    public async Task<WeatherResponse> GetCurrentWeatherAsync(double latitude, double longitude, string locationName, CancellationToken cancellationToken)
    {
        var url = $"/v1/forecast?latitude={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&current_weather=true&timezone=auto";
        try
        {
            using var resp = await _http.GetAsync(url, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Open-Meteo error {Status}: {Body}", resp.StatusCode, body);
                return new WeatherResponse { IsSuccess = false, ErrorMessage = $"Weather API error: {resp.StatusCode}" };
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("current_weather", out var cw))
            {
                var temp = cw.GetProperty("temperature").GetDouble();
                var windspeed = cw.GetProperty("windspeed").GetDouble();
                var time = cw.GetProperty("time").GetString() ?? DateTimeOffset.UtcNow.ToString();
                var obsTime = DateTimeOffset.Parse(time);

                var conditions = $"temperature {temp}°C, wind {windspeed} km/h";

                return new WeatherResponse
                {
                    IsSuccess = true,
                    Location = locationName,
                    TemperatureCelsius = temp,
                    WindSpeedKph = windspeed,
                    Conditions = conditions,
                    ObservationTime = obsTime
                };
            }
            else
            {
                _logger.LogWarning("Open-Meteo response missing current_weather: {Body}", body);
                return new WeatherResponse { IsSuccess = false, ErrorMessage = "Weather data missing" };
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            _logger.LogError(ex, "Weather API failed");
            return new WeatherResponse { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }
}