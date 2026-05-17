using System.Text.Json;
using System.Text.Json.Serialization;
using XiangqiAnalyzerApi.Middleware;
using XiangqiApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddEndpointsApiExplorer();  // Quan trọng: cái này cho minimal APIs
builder.Services.AddSwaggerGen();

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddSingleton<PikafishService>();

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Xiangqi Analyzer API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "Xiangqi Analyzer API Documentation";
});

app.MapGet("/", () => Results.Redirect("/swagger"));

app.UseCors("AllowAll");

//app.UseMiddleware<ApiKeyMiddleware>();

app.UseRouting();
app.MapControllers();
app.MapHealthChecks("/health");

// Ensure engine directory exists
var engineDir = Path.Combine(Directory.GetCurrentDirectory(), "Engines");
if (!Directory.Exists(engineDir))
{
    Directory.CreateDirectory(engineDir);
    Console.WriteLine($"Created engines directory at: {engineDir}");
    Console.WriteLine("Please place pikafish engine executable in this directory.");
}

app.Run();