using Microsoft.Extensions.AI;
using VoiceAgentApi.Models.Settings;
using VoiceAgentApi.Services.Implementations;
using VoiceAgentApi.Services.Interfaces;

namespace VoiceAgentApi.Extensions;

/// <summary>
/// Extension methods for IServiceCollection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds application services to the DI container
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register conversation services
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<ILlmService, LlmService>();

        // Register vector database service
        services.AddScoped<IVectorDatabaseService, VectorDatabaseService>();

        return services;
    }


    /// <summary>
    /// Adds external AI services (OpenAI, embedding generators, etc.)
    /// </summary>
    public static IServiceCollection AddExternalAIServices(this IServiceCollection services, IConfiguration configuration)
    {
        var openAiApiKey = configuration["OpenAI:ApiKey"];
        var embeddingModel = configuration["OpenAI:EmbeddingModel"];

        // Register IEmbeddingGenerator using OpenAI
        #pragma warning disable SKEXP0010
        services.AddOpenAIEmbeddingGenerator(embeddingModel!, openAiApiKey!);
        #pragma warning restore SKEXP0010

        return services;
    }

    /// <summary>
    /// Adds and validates configuration settings
    /// </summary>
    public static IServiceCollection AddApplicationSettings(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure OpenAI settings with validation
        services.Configure<OpenAISettings>(
            configuration.GetSection(OpenAISettings.SectionName))
            .AddOptions<OpenAISettings>()
            .Validate(settings =>
            {
                try
                {
                    settings.Validate();
                    return true;
                }
                catch
                {
                    return false;
                }
            }, "OpenAI settings validation failed")
            .ValidateOnStart();

        // Configure Pinecone settings with validation
        services.Configure<PineconeSettings>(
            configuration.GetSection(PineconeSettings.SectionName))
            .AddOptions<PineconeSettings>()
            .Validate(settings =>
            {
                try
                {
                    settings.Validate();
                    return true;
                }
                catch
                {
                    return false;
                }
            }, "Pinecone settings validation failed")
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Adds CORS policies
    /// </summary>
    public static IServiceCollection AddCorsConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                // TODO: In production, configure this with specific origins from appsettings
                var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                    ?? new[] { "http://localhost:3000", "http://localhost:5173" };

                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        return services;
    }

    /// <summary>
    /// Adds API versioning support
    /// </summary>
    public static IServiceCollection AddApiVersioningConfiguration(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }
}
