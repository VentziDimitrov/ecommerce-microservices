using System.Net.WebSockets;
using VoiceAgentApi.Handlers;
using VoiceAgentApi.Services.Interfaces;

namespace VoiceAgentApi.Middleware;

/// <summary>
/// Middleware for handling WebSocket connections
/// </summary>
public class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WebSocketMiddleware> _logger;

    public WebSocketMiddleware(RequestDelegate next, ILogger<WebSocketMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration, IServiceProvider serviceProvider)
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
            _logger.LogInformation("WebSocket connected from {RemoteIpAddress}", context.Connection.RemoteIpAddress);

            // Create a scope for scoped services
            using var scope = serviceProvider.CreateScope();
            var conversationService = scope.ServiceProvider.GetRequiredService<IConversationService>();
            var handlerLogger = scope.ServiceProvider.GetRequiredService<ILogger<VoiceSessionHandler>>();

            var session = new VoiceSessionHandler(webSocket, configuration, conversationService, handlerLogger);
            await session.HandleSessionAsync();

            _logger.LogInformation("WebSocket session ended");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket error");

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
    }
}
