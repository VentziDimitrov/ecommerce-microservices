namespace VoiceAgentApi.Models.DTOs;

/// <summary>
/// Represents a message in a conversation
/// </summary>
public class Message
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
