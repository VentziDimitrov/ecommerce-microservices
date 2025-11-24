using System.Text;
using VoiceAgentApi.Models.DTOs;
using VoiceAgentApi.Services.Interfaces;

namespace VoiceAgentApi.Services.Implementations;

/// <summary>
/// Manages conversation history for a session
/// </summary>
public class SessionHistory : ISessionHistory
{
    private readonly List<Message> _messages = new();
    private readonly object _lock = new();

    public void AddUserUtterance(string text)
    {
        lock (_lock)
        {
            _messages.Add(new Message { Role = "user", Content = text });
        }
    }

    public void AddAgentResponse(string text)
    {
        lock (_lock)
        {
            _messages.Add(new Message { Role = "assistant", Content = text });
        }
    }

    public List<Message> GetMessages()
    {
        lock (_lock)
        {
            return new List<Message>(_messages);
        }
    }

    public string GetFormattedHistory(int maxMessages = 10)
    {
        lock (_lock)
        {
            var recentMessages = _messages.TakeLast(maxMessages);
            var sb = new StringBuilder();
            foreach (var msg in recentMessages)
            {
                sb.AppendLine($"{msg.Role}: {msg.Content}");
            }
            return sb.ToString();
        }
    }
}
