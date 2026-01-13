using LightShield.Api.Data;
using LightShield.Api.Services;
using LightShield.Api.Services.Alerts;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using DotNetEnv;

// ===================================================================
//  Load Embedded .env
// ===================================================================

var assembly = Assembly.GetExecutingAssembly();

// IMPORTANT: list all embedded resources to confirm name
Console.WriteLine("Embedded resources:");
foreach (var r in assembly.GetManifestResourceNames())
    Console.WriteLine(" - " + r);

// The correct name will be "LightShield.Api._env" for a file named ".env" . 
var resource = assembly.GetManifestResourceStream("LightShield.Api..env");

if (resource == null)
{
    Console.WriteLine("ERROR: Embedded .env NOT FOUND!");
}
else
{
    Env.Load(resource);
    Console.WriteLine("Loaded embedded .env");
}

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
builder.Services.AddScoped<SmtpAlertService>();
builder.Services.AddScoped<IAlertService, CompositeAlertService>();
builder.Services.AddScoped<AlertWriterService>();

// Background anomaly detection
builder.Services.AddHostedService<AnomalyDetectionService>();

// Configuration management
builder.Services.AddScoped<ConfigurationService>();

builder.Services.AddScoped<FileBaselineService>();

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowAnyOrigin();
    });
});

builder.Services.AddEndpointsApiExplorer();

// ===================================================================
//  Build App
// ===================================================================

var app = builder.Build();

// Run DB migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
    db.Database.Migrate();

    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    db.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(TRUNCATE);");
}

app.UseCors();
app.MapControllers();
app.Run();
