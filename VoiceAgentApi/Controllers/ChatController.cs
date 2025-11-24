using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using VoiceAgentApi.Services.Interfaces;

namespace VoiceAgentApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ILogger<ChatController> _logger;
    private readonly IVectorDatabaseService _vectorDatabaseService;
    

    public ChatController(
        ILogger<ChatController> logger,
        IVectorDatabaseService vectorDatabaseService
        )
    {
        _vectorDatabaseService = vectorDatabaseService;
        _logger = logger;
    }

    
}
