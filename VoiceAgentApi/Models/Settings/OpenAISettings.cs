namespace VoiceAgentApi.Models.Settings;

public class OpenAISettings
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;
    public string ChatModel { get; set; } = "gpt-4";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public double Temperature { get; set; } = 0.2;
    public int MaxTokens { get; set; } = 500;
    public double TopP { get; set; } = 1.0;

    /// <summary>
    /// Validates the configuration settings
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("OpenAI ApiKey is required. Please configure it in appsettings.json");

        if (Temperature < 0 || Temperature > 2)
            throw new InvalidOperationException($"Temperature must be between 0 and 2, got {Temperature}");

        if (MaxTokens <= 0)
            throw new InvalidOperationException($"MaxTokens must be positive, got {MaxTokens}");

        if (TopP < 0 || TopP > 1)
            throw new InvalidOperationException($"TopP must be between 0 and 1, got {TopP}");
    }
}
