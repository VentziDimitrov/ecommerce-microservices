using System.Net.WebSockets;
using Microsoft.Extensions.AI;
using backend.Services;

var builder = WebApplication.CreateBuilder(args);

// --- AI Services Configuration ---
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];
var embeddingModel = builder.Configuration["OpenAI:EmbeddingModel"];

builder.Services.AddControllers();

builder.Services.AddScoped<IVectorDatabaseService, VectorDatabaseService>();

// Register IEmbeddingGenerator using OpenAI
#pragma warning disable SKEXP0010
builder.Services.AddOpenAIEmbeddingGenerator(embeddingModel, openAiApiKey!);
#pragma warning restore SKEXP0010

// Add CORS support
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.Configure<OpenAISettings>(
    builder.Configuration.GetSection(OpenAISettings.SectionName))
    .AddOptions<OpenAISettings>()
    .Validate(settings =>
    {
        try
        {
            settings.Validate();
            return true;
        }
        catch
        {
            return false;
        }
    }, "OpenAI settings validation failed")
    .ValidateOnStart();

builder.Services.Configure<PineconeSettings>(
    builder.Configuration.GetSection(PineconeSettings.SectionName))
    .AddOptions<PineconeSettings>()
    .Validate(settings =>
    {
        try
        {
            settings.Validate();
            return true;
        }
        catch
        {
            return false;
        }
    }, "Pinecone settings validation failed")
    .ValidateOnStart();

var app = builder.Build();

// Configure graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("Application is shutting down...");
});

// Enable CORS
app.UseCors("AllowAll");

// Configure WebSocket options
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(120)
};
app.UseWebSockets(webSocketOptions);

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// WebSocket endpoint for voice sessions
app.Map("/voice/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("WebSocket connection required");
        return;
    }

    WebSocket? webSocket = null;
    try
    {
        webSocket = await context.WebSockets.AcceptWebSocketAsync();
        Console.WriteLine($"WebSocket connected from {context.Connection.RemoteIpAddress}");

        var session = new VoiceSessionHandler(webSocket, builder.Configuration);
        await session.HandleSessionAsync();

        Console.WriteLine("WebSocket session ended");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"WebSocket error: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");

        // If WebSocket is already open, we can't set HTTP status code
        // Instead, close the WebSocket with an error
        if (webSocket?.State == WebSocketState.Open)
        {
            await webSocket.CloseAsync(
                WebSocketCloseStatus.InternalServerError,
                $"Error: {ex.Message}",
                CancellationToken.None);
        }
    }
});

// Map controller endpoints
app.MapControllers();

Console.WriteLine("Voice Agent API starting...");
Console.WriteLine($"Listening on: {builder.Configuration["urls"] ?? "http://localhost:5000"}");
app.Run();
