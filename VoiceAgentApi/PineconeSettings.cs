/// <summary>
/// Configuration settings for Pinecone vector database
/// </summary>
public class PineconeSettings
{
    public const string SectionName = "Pinecone";

    public string ApiKey { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public string Model { get; set; } = "text-embedding-3-small";
    public int Dimension { get; set; } = 1536;
    public string Namespace { get; set; } = "__default__";

    public string IndexName {get; init;} = "bulgariaair";

    /// <summary>
    /// Validates the configuration settings
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("Pinecone ApiKey is required. Please configure it in appsettings.json");

        if (Dimension <= 0)
            throw new InvalidOperationException($"Pinecone Dimension must be positive, got {Dimension}");

        if (string.IsNullOrWhiteSpace(Region))
            throw new InvalidOperationException("Pinecone Region is required");
    }
}