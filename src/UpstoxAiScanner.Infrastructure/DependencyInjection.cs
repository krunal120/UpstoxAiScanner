using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using UpstoxAiScanner.Application.Configuration;
using UpstoxAiScanner.Application.Interfaces;
using UpstoxAiScanner.Infrastructure.Data;
using UpstoxAiScanner.Infrastructure.Services;
using UpstoxAiScanner.Infrastructure.Upstox.Services;

namespace UpstoxAiScanner.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Database Context
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=UpstoxAiScannerDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

        services.AddDbContext<UpstoxScannerDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(UpstoxScannerDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            });
        });

        // Bind Upstox options with environment variable fallback
        services.Configure<UpstoxOptions>(options =>
        {
            configuration.GetSection(UpstoxOptions.SectionName).Bind(options);

            // Fallback to UPSTOX_ACCESS_TOKEN environment variable if not already set in configuration/secrets
            if (string.IsNullOrWhiteSpace(options.AccessToken))
            {
                var envToken = Environment.GetEnvironmentVariable("UPSTOX_ACCESS_TOKEN");
                if (!string.IsNullOrWhiteSpace(envToken))
                {
                    options.AccessToken = envToken;
                }
            }
        });

        services.AddHttpContextAccessor();

        // Register rate limiter and mock data provider as singletons
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<UpstoxOptions>>().CurrentValue;
            return new UpstoxRateLimiter(options.ThrottleRequestsPerSecond);
        });

        services.AddSingleton<MockUpstoxDataProvider>();

        // Register typed HttpClient with Polly retry policies
        services.AddHttpClient<IUpstoxMarketDataService, UpstoxApiClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptionsMonitor<UpstoxOptions>>().CurrentValue;
            var baseUri = options.BaseUrl.TrimEnd('/') + "/";
            client.BaseAddress = new Uri(baseUri);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        })
        .AddPolicyHandler((serviceProvider, request) =>
        {
            var options = serviceProvider.GetRequiredService<IOptionsMonitor<UpstoxOptions>>().CurrentValue;
            var logger = serviceProvider.GetRequiredService<ILogger<UpstoxApiClient>>();

            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == (HttpStatusCode)429) // 429 Too Many Requests
                .WaitAndRetryAsync(
                    options.MaxRetryAttempts,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryAttempt, context) =>
                    {
                        logger.LogWarning(
                            "Delaying for {Delay}s, then making retry {Retry} for {Uri} due to {StatusCode}",
                            timespan.TotalSeconds, retryAttempt, request.RequestUri, outcome.Result?.StatusCode);
                    });
        });

        // Register Instrument Service
        services.AddHttpClient<IInstrumentService, InstrumentService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
        });

        // Register Technical Indicator Analysis Service
        services.AddScoped<ITechnicalAnalysisService, TechnicalAnalysisService>();

        // Register Scanner Service
        services.AddScoped<IScannerService, ScannerService>();

        // Configure Scanner Background options
        services.Configure<ScannerBackgroundOptions>(options =>
        {
            configuration.GetSection(ScannerBackgroundOptions.SectionName).Bind(options);
        });

        // Register Market Hours Service
        services.AddSingleton<IMarketHoursService, MarketHoursService>();

        // Register WebSocket Market Data Client
        services.AddSingleton<IUpstoxWebSocketClient, UpstoxWebSocketClient>();

        // Register Background Scanner Hosted Service & Controller interface
        services.AddSingleton<MarketScannerBackgroundService>();
        services.AddHostedService(sp => sp.GetRequiredService<MarketScannerBackgroundService>());
        services.AddSingleton<IBackgroundScannerControllerService>(sp => sp.GetRequiredService<MarketScannerBackgroundService>());

        // Configure AI Explanation options with environment variable fallback
        services.Configure<AiOptions>(options =>
        {
            configuration.GetSection(AiOptions.SectionName).Bind(options);

            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                var envKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (!string.IsNullOrWhiteSpace(envKey))
                {
                    options.ApiKey = envKey;
                }
            }
        });

        // Register AI Explanation Service with typed HttpClient
        services.AddHttpClient<IAiExplanationService, AiExplanationService>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<AiOptions>>().CurrentValue;
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        return services;
    }
}
