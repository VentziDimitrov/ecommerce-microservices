using System.Text;
using OpenAI;
using OpenAI.Chat;
using VoiceAgentApi.Models.DTOs;
using VoiceAgentApi.Services.Interfaces;

namespace VoiceAgentApi.Services.Implementations;

/// <summary>
/// Service for Large Language Model operations
/// </summary>
public class LlmService : ILlmService
{
    private readonly IConfiguration _configuration;
    private readonly OpenAIClient _client;
    private readonly ILogger<LlmService> _logger;

    public LlmService(
        IConfiguration configuration,
        ILogger<LlmService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        string apiKey = configuration["OpenAI:ApiKey"] ?? throw new ArgumentNullException("OpenAI:ApiKey");
        _client = new OpenAIClient(apiKey);
    }

    public async Task<string> CorrectQuestion(string question, SessionHistory history)
    {
        var prompt = BuildCorrectionSystemPrompt(question, history);
        var chatClient = _client.GetChatClient("gpt-4o-mini");
        ChatCompletionOptions options = new ChatCompletionOptions
        {
            Temperature = 0.2f,
            MaxOutputTokenCount = 500,
            TopP = 1.0f,
        };

        var result = await chatClient.CompleteChatAsync([new SystemChatMessage(prompt)], options);

        _logger.LogInformation("OpenAI response: {Response}", result.Value.Content[0].Text);
        if (result.Value.Content[0].Text != null)
        {
            return result.Value.Content[0].Text;
        }
        return question;
    }

    public async Task<string> GenerateAnswerAsync(SessionHistory history, List<RetrievedDocument> contextChunks, string userQuery)
    {
        var llmEndpoint = _configuration["LLM:Endpoint"];
        if (string.IsNullOrEmpty(llmEndpoint))
        {
            // Fallback to simple echo response
            return $"I understand you said: '{userQuery}'.";
        }

        try
        {
            // Build prompt with context and history
            // Commented out pending full implementation
            /*var prompt = BuildPrompt(history, contextChunks, userQuery);

            // Call LLM API (example for Azure OpenAI)
            var requestBody = new
            {
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful AI assistant." },
                    new { role = "user", content = prompt }
                },
                max_tokens = 150,
                temperature = 0.7
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(llmEndpoint, content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

            // Extract the generated text from response
            if (jsonResponse.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var messageContent))
                {
                    return messageContent.GetString() ?? "I apologize, but I couldn't generate a response.";
                }
            }

            return "I apologize, but I couldn't generate a response."; */
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling LLM");
            return $"I understand your question about '{userQuery}', but I'm having trouble generating a response right now.";
        }

        return "I apologize, but I couldn't generate a response.";
    }

    private string BuildPrompt(SessionHistory history, List<string> contextChunks, string userQuery)
    {
        var sb = new StringBuilder();

        if (contextChunks.Any())
        {
            sb.AppendLine("Relevant context:");
            foreach (var chunk in contextChunks)
            {
                sb.AppendLine($"- {chunk}");
            }
            sb.AppendLine();
        }

        var recentHistory = history.GetFormattedHistory(5);
        if (!string.IsNullOrEmpty(recentHistory))
        {
            sb.AppendLine("Conversation history:");
            sb.AppendLine(recentHistory);
            sb.AppendLine();
        }

        sb.AppendLine($"User question: {userQuery}");
        sb.AppendLine("Please provide a helpful and concise response.");

        return sb.ToString();
    }

    private string BuildCorrectionSystemPrompt(string question, SessionHistory history)
    {
        var recentHistory = history.GetFormattedHistory(10);
        var prompt = $"""
        ## Role and Context
        You are a specialized speech correction AI supporting Bulgaria Air's customer service assistant. Your function is to process speech-to-text output in Bulgarian and ensure accurate interpretation of customer inquiries.

        ## Primary Objectives
        - Analyze input text for speech recognition errors
        - Correct misheard words, phrases, and grammatical mistakes
        - Remove duplicate words or phrases
        - Produce clear, coherent, and contextually accurate output

        ## Input Context
        Conversation history is provided to inform your corrections and maintain contextual accuracy.

        ## Language
        All input and output text is in Bulgarian.

        ## Output Format
        Return only the corrected text without explanations, comments, or metadata.

        ---

        CONVERSATION HISTORY:
        {recentHistory}

        TEXT TO CORRECT:
        {question}
        """;

        return prompt;
    }
}
