using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WeatherAgentConsole.Services;

public class AnthropicClient
{
    private readonly HttpClient _client;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<AnthropicClient> _logger;

    public AnthropicClient(IHttpClientFactory httpFactory, IConfiguration config, ILogger<AnthropicClient> logger)
    {
        _client = httpFactory.CreateClient("anthropic");
        _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? config.GetValue<string>("Anthropic:ApiKey") ?? throw new InvalidOperationException("Anthropic API key missing. Set ANTHROPIC_API_KEY.");
        _model = config.GetValue<string>("Anthropic:Model") ?? "claude-2.1";
        _logger = logger;

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<string> SendConversationAsync(string prompt, CancellationToken cancellationToken)
    {
        var endpoint = new Uri(_client.BaseAddress!, "/v1/responses");

        var body = new
        {
            model = _model,
            input = prompt,
            max_tokens_to_sample = 1000,
            temperature = 0.0
        };

        var json = JsonSerializer.Serialize(body);
        _logger.LogDebug("Anthropic request: {Json}", json);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await _client.PostAsync(endpoint, content, cancellationToken);
        var respText = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic API error {StatusCode}: {Response}", resp.StatusCode, respText);
            throw new HttpRequestException($"Anthropic API returned {resp.StatusCode}: {respText}");
        }

        try
        {
            using var doc = JsonDocument.Parse(respText);
            if (doc.RootElement.TryGetProperty("output", out var output))
            {
                if (output.ValueKind == JsonValueKind.Object && output.TryGetProperty("text", out var text))
                {
                    return text.GetString() ?? "";
                }
                else if (output.ValueKind == JsonValueKind.String)
                {
                    return output.GetString() ?? "";
                }
            }

            if (doc.RootElement.TryGetProperty("completion", out var completion)) return completion.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("text", out var text2)) return text2.GetString() ?? "";

            return respText;
        }
        catch (JsonException)
        {
            _logger.LogWarning("Anthropic response not JSON - returning raw string");
            return respText;
        }
    }
}