using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WeatherAgentConsole.Services;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        cfg.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddHttpClient("anthropic", client =>
        {
            client.BaseAddress = new Uri(context.Configuration.GetValue<string>("Anthropic:BaseUrl") ?? "https://api.anthropic.com");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient("geocoding", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddHttpClient("open-meteo", client =>
        {
            client.BaseAddress = new Uri(context.Configuration.GetValue<string>("OpenMeteo:BaseUrl") ?? "https://api.open-meteo.com");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddSingleton<AnthropicClient>();
        services.AddSingleton<GeocodingService>();
        services.AddSingleton<WeatherService>();
        services.AddSingleton<ToolHandler>();
        services.AddSingleton<WeatherAgent>();

        services.AddLogging(cfg => cfg.AddConsole());
    })
    .Build();

using var cts = new CancellationTokenSource();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    await host.StartAsync(cts.Token);

    var agent = host.Services.GetRequiredService<WeatherAgent>();
    logger.LogInformation("Weather AI Agent (console) started. Type a question or 'exit' to quit.");

    while (true)
    {
        Console.Write("> ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)) continue;
        if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

        try
        {
            using var userCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
            userCts.CancelAfter(TimeSpan.FromSeconds(60));
            var reply = await agent.HandleUserInputAsync(input, userCts.Token);
            Console.WriteLine(reply);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Operation cancelled (timeout).");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling user input");
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
finally
{
    await host.StopAsync();
}