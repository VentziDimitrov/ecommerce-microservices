using System.Text;
using System.Text.Json;
using OpenAI;
using OpenAI.Chat;

public class ConversationService
{
    private readonly RetrievalService _retrievalService;
    private readonly LlmService _llmService;
    private readonly SessionHistory _history;

    public ConversationService(IConfiguration configuration)
    {
        _retrievalService = new RetrievalService(configuration);
        _llmService = new LlmService(configuration);
        _history = new SessionHistory();
    }

    public async Task<string> ProcessUserTextAsync(string userText)
    {
        _history.AddUserUtterance(userText);

        // Retrieve relevant context from knowledge base
        var chunks = await _retrievalService.QueryAsync(userText);

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

public class SessionHistory
{
    private readonly List<Message> _messages = new();

    public void AddUserUtterance(string text)
    {
        _messages.Add(new Message { Role = "user", Content = text });
    }

    public void AddAgentResponse(string text)
    {
        _messages.Add(new Message { Role = "assistant", Content = text });
    }

    public List<Message> GetMessages() => _messages;

    public string GetFormattedHistory(int maxMessages = 10)
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

public class Message
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class RetrievalService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public RetrievalService(IConfiguration configuration)
    {
        _configuration = configuration;
        _httpClient = new HttpClient();

        var pineconeApiKey = _configuration["Pinecone:ApiKey"];
        if (!string.IsNullOrEmpty(pineconeApiKey) && pineconeApiKey != "YOUR_API_KEY")
        {
            _httpClient.DefaultRequestHeaders.Add("Api-Key", pineconeApiKey);
        }
    }

    public Task<List<string>> QueryAsync(string query)
    {
        // Placeholder implementation
        // In a real implementation, you would:
        // 1. Convert query to embeddings using Azure OpenAI or similar
        // 2. Query Pinecone vector database
        // 3. Return relevant chunks

        var pineconeApiKey = _configuration["Pinecone:ApiKey"];
        if (string.IsNullOrEmpty(pineconeApiKey) || pineconeApiKey == "YOUR_API_KEY")
        {
            Console.WriteLine("Pinecone not configured, returning empty context");
            return Task.FromResult(new List<string>());
        }

        try
        {
            // TODO: Implement actual Pinecone query
            // For now, return empty list
            return Task.FromResult(new List<string>());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error querying knowledge base: {ex.Message}");
            return Task.FromResult(new List<string>());
        }
    }
}

public class LlmService
{
    private readonly IConfiguration _configuration;
    private readonly OpenAIClient _client;

    public LlmService(IConfiguration configuration)
    {
        _configuration = configuration;
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

        Console.WriteLine("OpenAI response: " + result.Value.Content[0].Text);
        if (result.Value.Content[0].Text != null)
        {
            return result.Value.Content[0].Text;
        }
        return question;
    }

    public async Task<string> GenerateAnswerAsync(SessionHistory history, List<string> contextChunks, string userQuery)
    {
        var llmEndpoint = _configuration["LLM:Endpoint"];
        if (string.IsNullOrEmpty(llmEndpoint) || llmEndpoint == "YOUR_LLM_ENDPOINT")
        {
            // Fallback to simple echo response
            return $"I understand you said: '{userQuery}'.";
        }

        try
        {
            // Build prompt with context and history
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
            Console.WriteLine($"Error calling LLM: {ex.Message}");
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
