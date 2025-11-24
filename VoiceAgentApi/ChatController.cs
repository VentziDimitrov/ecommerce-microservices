using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using backend.Services;
using Microsoft.Extensions.AI;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ILogger<ChatController> _logger;
    private readonly IVectorDatabaseService _vectorDatabaseService;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    public ChatController(
        ILogger<ChatController> logger,
        IVectorDatabaseService vectorDatabaseService,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _vectorDatabaseService = vectorDatabaseService;
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
    }

    public async Task<Embedding<float>> GenerateEmbeddingAsync(string text)
    {
        try
        {            
            _logger.LogDebug("Generating embedding for text of length: {Length}", text.Length);
            var embeddingResult = await _embeddingGenerator.GenerateAsync(text);
            return embeddingResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding");
            throw;
        }
    }
}