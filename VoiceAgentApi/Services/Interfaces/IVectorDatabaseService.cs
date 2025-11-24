using Microsoft.Extensions.AI;
using VoiceAgentApi.Models.DTOs;

namespace VoiceAgentApi.Services.Interfaces;

public interface IVectorDatabaseService
{
    Task<List<RetrievedDocument>> SearchAsync(ReadOnlyMemory<float> vector, uint topK);
    Task StoreDocumentAsync(string documentId, string content, Embedding<float> embedding, Dictionary<string, object> metadata);
    Task<bool> DeleteDocumentAsync(string documentId);
    Task CreateIndexIfNotExists(string indexName);
    Microsoft.Extensions.AI.EmbeddingGenerationOptions GetEmbeddingOptions();
    Task<Embedding<float>> GenerateEmbeddingAsync(string text);
}
