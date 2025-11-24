namespace VoiceAgentApi.Services.Interfaces;

/// <summary>
/// Interface for managing conversation flow
/// </summary>
public interface IConversationService
{
    Task<string> ProcessUserTextAsync(string userText);
    Task<string> CorrectUserTextAsync(string userText);
}
