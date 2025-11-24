namespace VoiceAgentApi.Models.DTOs;

/// <summary>
/// Represents a document retrieved from vector search
/// </summary>
public record RetrievedDocument
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public float Score { get; init; }
}
