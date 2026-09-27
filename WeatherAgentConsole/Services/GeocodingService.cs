using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WeatherAgentConsole.Services;

public class GeocodingService
{
    private readonly HttpClient _http;
    private readonly string _userAgent;
    private readonly ILogger<GeocodingService> _logger;

    public GeocodingService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<GeocodingService> logger)
    {
        _http = httpFactory.CreateClient("geocoding");
        _userAgent = config.GetValue<string>("Geocoding:UserAgent") ?? "WeatherAgentConsole/1.0";
        _logger = logger;
    }

    public async Task<(double Lat, double Lon, string DisplayName)?> GeocodeAsync(string city, string? country, CancellationToken cancellationToken)
    {
        var query = string.IsNullOrWhiteSpace(country) ? city : $"{city}, {country}";
        var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit=1";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd(_userAgent);

        try
        {
            using var resp = await _http.SendAsync(req, cancellationToken);
            var text = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Geocoding API returned {Status}: {Body}", resp.StatusCode, text);
                return null;
            }

            var arr = JsonSerializer.Deserialize<JsonElement[]>(text);
            if (arr == null || arr.Length == 0) return null;

            var first = arr[0];
            if (first.TryGetProperty("lat", out var latEl) && first.TryGetProperty("lon", out var lonEl))
            {
                var lat = double.Parse(latEl.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                var lon = double.Parse(lonEl.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                var display = first.TryGetProperty("display_name", out var dn) ? dn.GetString() ?? query : query;
                return (lat, lon, display);
            }

            return null;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            _logger.LogError(ex, "Geocoding failed");
            return null;
        }
    }
}