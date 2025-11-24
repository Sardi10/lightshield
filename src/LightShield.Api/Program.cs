using LightShield.Api.Data;
using LightShield.Api.Services;
using LightShield.Api.Services.Alerts;
using Microsoft.EntityFrameworkCore;



// ===================================================================
//  Load Environment Variables
// ===================================================================
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
DotNetEnv.Env.Load(envPath);

Console.WriteLine("Loaded .env from: " + envPath);
Console.WriteLine("SMTP_HOST: " + Environment.GetEnvironmentVariable("SMTP_HOST"));

var builder = WebApplication.CreateBuilder(args);

// ===================================================================
//  Add Services
// ===================================================================

// Database
builder.Services.AddDbContext<EventsDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
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

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowAnyOrigin();
    });
});

// Swagger
builder.Services.AddEndpointsApiExplorer();


// ===================================================================
//  Build App
// ===================================================================
var app = builder.Build();

// Apply EF Core migrations & checkpoint
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
    db.Database.Migrate();

    // Enable WAL mode 
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    db.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(TRUNCATE);");
}

Console.WriteLine("SMTP FROM = " + builder.Configuration["ALERT_EMAIL_FROM"]);
Console.WriteLine("SMTP TO = " + builder.Configuration["ALERT_EMAIL_TO"]);
Console.WriteLine("SMTP HOST = " + builder.Configuration["SMTP_HOST"]);



app.UseCors();
app.MapControllers();

app.Run();
