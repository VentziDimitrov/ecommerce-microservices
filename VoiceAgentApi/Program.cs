using Serilog;
using VoiceAgentApi.Extensions;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/voiceagent-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting Voice Agent API");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog();

    // --- Service Configuration ---

    // Add controllers
    builder.Services.AddControllers();

    // Add application settings with validation
    builder.Services.AddApplicationSettings(builder.Configuration);

    // Add application services (conversation, LLM, retrieval, vector DB)
    builder.Services.AddApplicationServices(builder.Configuration);

    // Add external AI services (OpenAI embeddings, etc.)
    builder.Services.AddExternalAIServices(builder.Configuration);

    // Add CORS support
    builder.Services.AddCorsConfiguration(builder.Configuration);

    // Add API versioning
    builder.Services.AddApiVersioningConfiguration();

    // Add health checks
    builder.Services.AddHealthChecks();

    // --- Application Configuration ---

    var app = builder.Build();

    // Configure graceful shutdown
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStopping.Register(() =>
    {
        app.Logger.LogInformation("Application is shutting down...");
    });

    // Use global exception handling
    app.UseGlobalExceptionHandler();

    // Enable CORS
    app.UseCors("AllowFrontend");

    // Configure WebSocket endpoints
    app.UseWebSocketEndpoints();

    // Health check endpoint
    app.MapHealthChecks("/health");

    // Map controller endpoints
    app.MapControllers();

    app.Logger.LogInformation("Voice Agent API starting...");
    app.Logger.LogInformation("Listening on: {Urls}", builder.Configuration["urls"] ?? "http://localhost:5000");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
