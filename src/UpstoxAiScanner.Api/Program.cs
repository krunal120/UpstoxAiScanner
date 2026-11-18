using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using UpstoxAiScanner.Application.Interfaces;
using UpstoxAiScanner.Infrastructure;
using UpstoxAiScanner.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Load optional local uncommitted settings (API keys, secrets)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add Controllers with JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Add Infrastructure services (Upstox client, Polly policies, rate limiting, options)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add API Explorer and Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Upstox AI Stock Scanner API",
        Version = "v1",
        Description = "Production-grade Market Data & AI Stock Scanner backend integrating Upstox V3 APIs.",
        Contact = new OpenApiContact
        {
            Name = "Upstox AI Scanner Team"
        }
    });

    // Add Bearer Auth to Swagger UI for optional custom header testing
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Upstox Bearer Access Token. Enter 'Bearer {token}' or simply paste the raw token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML Comments in Swagger
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseDeveloperExceptionPage();
}

// Fix Swagger UI compatibility with Microsoft.OpenApi 1.6.23+ which outputs "openapi": "3.0.4"
// Older Swagger UI regex expects "3.0.1" or "3.0.3"
app.Use(async (context, next) =>
{
    if (context.Request.Path.Value != null && context.Request.Path.Value.EndsWith("swagger.json", StringComparison.OrdinalIgnoreCase))
    {
        var originalBody = context.Response.Body;
        using var memStream = new MemoryStream();
        context.Response.Body = memStream;

        await next();

        memStream.Position = 0;
        using var reader = new StreamReader(memStream);
        var json = await reader.ReadToEndAsync();

        json = json.Replace("\"openapi\": \"3.0.4\"", "\"openapi\": \"3.0.1\"")
                   .Replace("\"openapi\":\"3.0.4\"", "\"openapi\":\"3.0.1\"");

        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        context.Response.Body = originalBody;
        context.Response.ContentLength = bytes.Length;
        context.Response.ContentType = "application/json;charset=utf-8";
        await context.Response.Body.WriteAsync(bytes);
        return;
    }

    await next();
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Upstox AI Scanner API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");

app.UseRouting();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

// Fallback to React SPA index.html for non-API client routes
app.MapFallbackToFile("index.html");

// Apply database migrations on startup and seed Nifty 500 universe if empty
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = services.GetRequiredService<UpstoxScannerDbContext>();
        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync();
        }

        // Automatically seed stock universe if Instruments table is empty
        if (!await db.Instruments.AnyAsync())
        {
            logger.LogInformation("Instruments table is empty. Automatically synchronizing ALL NSE & BSE stock universe...");
            var instrumentService = services.GetRequiredService<IInstrumentService>();
            await instrumentService.SyncUniverseInstrumentsAsync("ALL");
            logger.LogInformation("Stock universe successfully synchronized.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying database migrations or seeding universe.");
    }
}

app.Run();

// Required for WebApplicationFactory in Integration Tests
public partial class Program { }
