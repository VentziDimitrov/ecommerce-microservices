using VoiceAgentApi.Middleware;

namespace VoiceAgentApi.Extensions;

/// <summary>
/// Extension methods for IApplicationBuilder
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds global exception handling middleware
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }

    /// <summary>
    /// Configures WebSocket support and maps WebSocket endpoints
    /// </summary>
    public static IApplicationBuilder UseWebSocketEndpoints(this IApplicationBuilder app)
    {
        var webSocketOptions = new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(120)
        };

        app.UseWebSockets(webSocketOptions);

        // Map WebSocket endpoint for voice sessions
        app.Map("/voice/ws", wsApp =>
        {
            wsApp.UseMiddleware<WebSocketMiddleware>();
        });

        return app;
    }
}
