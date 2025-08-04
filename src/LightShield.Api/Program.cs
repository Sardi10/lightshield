using LightShield.Api.Data;
using LightShield.Api.Services;
using Microsoft.EntityFrameworkCore;
using LightShield.Api.Services.Alerts;
using LightShield.Api.Models;
using DotNetEnv;
using Microsoft.Data.Sqlite;
using System.IO;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add CORS before AddControllers:
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDev", policy =>
    {
        policy.WithOrigins("http://localhost:5173",
                           "http://127.0.0.1:5173",
                           "tauri://localhost"
                           )  // your Vite dev URL
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 1) Register controllers
builder.Services.AddControllers();

// Alerting: register both SMS and SMTP, then composite
builder.Services.AddScoped<TwilioAlertService>();
builder.Services.AddScoped<SmtpAlertService>();
builder.Services.AddScoped<IAlertService, CompositeAlertService>();
// Hosted service for burst detection (will use the composite)
builder.Services.AddHostedService<AnomalyDetectionService>();

var contentRoot = Directory.GetCurrentDirectory();
var dbPath = Path.Combine(contentRoot, "lightshield.db");
var connBuilder = new SqliteConnectionStringBuilder { DataSource = dbPath };

// Register SQLite
builder.Services.AddDbContext<EventsDbContext>(opts =>
    opts.UseSqlite(connBuilder.ToString()));


// 2) (Optional) Keep OpenAPI/Swagger if you like
builder.Services.AddOpenApi();


var app = builder.Build();

// Use CORS *before* UseAuthorization:
app.UseCors("LocalDev");

// Ensure DB & migrations are applied on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

    // Force WAL  main file merge
    db.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(FULL);");

    // Apply any pending migrations
    db.Database.Migrate();
}

// 3) Enable Swagger UI in development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 4) Enforce HTTPS 
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}


// 5) Map controller routes
app.MapControllers();


app.Run();
