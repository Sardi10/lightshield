using LightShield.Api.Data;
using LightShield.Api.Services;
using LightShield.Api.Services.Alerts;
using Microsoft.EntityFrameworkCore;
using LightShield.Api.Security;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

// ===================================================================
//  Configure ProgramData Database Path
// ===================================================================

var dataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "LightShield"
);

Directory.CreateDirectory(dataDir);

var dbPath = Path.Combine(dataDir, "events.db");
Console.WriteLine("Using DB at: " + dbPath);

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5213);
});

// ===================================================================
//  Add Services
// ===================================================================

// Database using ProgramData path
builder.Services.AddDbContext<EventsDbContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
});

// Alerting Services 
builder.Services.AddScoped<TwilioAlertService>();
builder.Services.AddScoped<TelegramAlertService>();
builder.Services.AddScoped<SmtpAlertService>();
builder.Services.AddScoped<IAlertService, CompositeAlertService>();
builder.Services.AddScoped<AlertWriterService>();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("GeoIp", client => client.Timeout = TimeSpan.FromSeconds(2));
builder.Services.AddMemoryCache(options => options.SizeLimit = 10_000);


// Background anomaly detection
builder.Services.AddHostedService<AnomalyDetectionService>();

// Configuration management
builder.Services.AddScoped<ConfigurationService>();

builder.Services.AddScoped<FileBaselineService>();
builder.Services.AddSingleton<ApiKeyProvider>();
builder.Services.AddSingleton<SecretProtector>();
builder.Services.AddSingleton<GeoIpService>();
builder.Services.AddDataProtection()
    .SetApplicationName("LightShield")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")))
    .ProtectKeysWithDpapi(protectToLocalMachine: true);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("tauri://localhost", "http://tauri.localhost", "https://tauri.localhost", "http://localhost:5173")
              .WithHeaders("Content-Type", ApiKeyProvider.HeaderName)
              .AllowAnyMethod()
              .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});

builder.Services.AddEndpointsApiExplorer();

// ===================================================================
//  Build App
// ===================================================================

var app = builder.Build();

// Create the shared local credential before collectors or the desktop attempt to read it.
_ = app.Services.GetRequiredService<ApiKeyProvider>();

// Run DB migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
    db.Database.Migrate();

    var secretProtector = scope.ServiceProvider.GetRequiredService<SecretProtector>();
    var configuration = db.Configurations.FirstOrDefault();
    if (configuration != null &&
        (secretProtector.NeedsProtection(configuration.TelegramBotToken) ||
         secretProtector.NeedsProtection(configuration.TelegramChatId)))
    {
        configuration.TelegramBotToken = secretProtector.Protect(configuration.TelegramBotToken);
        configuration.TelegramChatId = secretProtector.Protect(configuration.TelegramChatId);
        db.SaveChanges();
    }

    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    db.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(TRUNCATE);");
}

app.UseCors();
app.Use(async (context, next) =>
{
    if (HttpMethods.IsOptions(context.Request.Method))
    {
        await next();
        return;
    }
    var apiKeyProvider = context.RequestServices.GetRequiredService<ApiKeyProvider>();
    var candidate = context.Request.Headers[ApiKeyProvider.HeaderName].FirstOrDefault();
    if (!apiKeyProvider.IsValid(candidate))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "A valid LightShield API credential is required." });
        return;
    }
    await next();
});
app.MapControllers();
app.Run();
