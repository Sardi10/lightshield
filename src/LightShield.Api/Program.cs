var builder = WebApplication.CreateBuilder(args);

// 1) Register controllers
builder.Services.AddControllers();

// 2) (Optional) Keep OpenAPI/Swagger if you like
builder.Services.AddOpenApi();

var app = builder.Build();

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
