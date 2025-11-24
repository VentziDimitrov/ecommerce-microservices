using VoiceAgentApi.Services.Interfaces;

namespace VoiceAgentApi.Services.Implementations;

/// <summary>
/// Service for managing conversation flow
/// </summary>
public class ConversationService : IConversationService
{
    private readonly IVectorDatabaseService _vectorDatabaseService;
    private readonly ILlmService _llmService;
    private readonly SessionHistory _history;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(
        IVectorDatabaseService vectorDatabaseService,
        ILlmService llmService,
        ILogger<ConversationService> logger)
    {
        _vectorDatabaseService = vectorDatabaseService;
        _llmService = llmService;
        _logger = logger;
        _history = new SessionHistory();
    }

    public async Task<string> ProcessUserTextAsync(string userText)
    {
        _history.AddUserUtterance(userText);

        var vector = await _vectorDatabaseService.GenerateEmbeddingAsync(userText);

        // Retrieve relevant context from knowledge base
        var chunks = await _vectorDatabaseService.SearchAsync(vector.Vector, 3);

        // Generate response using LLM
        var answer = await _llmService.GenerateAnswerAsync(_history, chunks, userText);

        _history.AddAgentResponse(answer);
        return answer;
    }

    public async Task<string> CorrectUserTextAsync(string userText)
    {
        var correctedText = await _llmService.CorrectQuestion(userText, _history);
        return correctedText;
    }
}
