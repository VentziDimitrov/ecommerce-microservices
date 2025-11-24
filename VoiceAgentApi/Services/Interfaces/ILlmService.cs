using VoiceAgentApi.Models.DTOs;
using VoiceAgentApi.Services.Implementations;

namespace VoiceAgentApi.Services.Interfaces;

/// <summary>
/// Interface for LLM (Large Language Model) operations
/// </summary>
public interface ILlmService
{
    Task<string> CorrectQuestion(string question, SessionHistory history);
    Task<string> GenerateAnswerAsync(SessionHistory history, List<RetrievedDocument> contextChunks, string userQuery);
}
