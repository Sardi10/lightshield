using LightShield.Api.Data;
using LightShield.Api.Services;
using Microsoft.EntityFrameworkCore;
using LightShield.Api.Services.Alerts;
using DotNetEnv;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// 1) Register controllers
builder.Services.AddControllers();

// Alerting: register both SMS and SMTP, then composite
builder.Services.AddScoped<TwilioAlertService>();
builder.Services.AddScoped<SmtpAlertService>();
builder.Services.AddScoped<IAlertService, CompositeAlertService>();
// Hosted service for burst detection (will use the composite)
builder.Services.AddHostedService<AnomalyDetectionService>();

// Register SQLite
builder.Services.AddDbContext<EventsDbContext>(opts =>
    opts.UseSqlite("Data Source=lightshield.db"));


// 2) (Optional) Keep OpenAPI/Swagger if you like
builder.Services.AddOpenApi();


var app = builder.Build();

// Ensure DB & migrations are applied on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
    db.Database.Migrate();
}

// 3) Enable Swagger UI in development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 4) Enforce HTTPS (already there)
app.UseHttpsRedirection();

// 5) Map controller routes
app.MapControllers();


app.Run();
