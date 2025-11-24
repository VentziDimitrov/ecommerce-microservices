using VoiceAgentApi.Models.DTOs;

namespace VoiceAgentApi.Services.Interfaces;

/// <summary>
/// Interface for managing conversation history
/// </summary>
public interface ISessionHistory
{
    void AddUserUtterance(string text);
    void AddAgentResponse(string text);
    List<Message> GetMessages();
    string GetFormattedHistory(int maxMessages = 10);
}
