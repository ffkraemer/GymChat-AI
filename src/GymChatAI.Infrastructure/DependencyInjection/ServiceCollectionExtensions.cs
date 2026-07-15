using GymChatAI.Application.Abstractions;
using GymChatAI.Application.Messaging;
using GymChatAI.Infrastructure.Ai;
using GymChatAI.Infrastructure.AI;
using GymChatAI.Infrastructure.LanguageDetection;
using GymChatAI.Infrastructure.Options;
using GymChatAI.Infrastructure.Persistence;
using GymChatAI.Infrastructure.WhatsApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GymChatAI.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGymChatInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.Configure<AzureOpenAIOptions>(configuration.GetSection(AzureOpenAIOptions.SectionName));
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));

        services.AddSingleton<InMemoryDataStore>();
        services.AddSingleton<IGymRepository, InMemoryGymRepository>();
        services.AddSingleton<IConversationRepository, InMemoryConversationRepository>();
        services.AddSingleton<IFaqRepository, InMemoryFaqRepository>();
        services.AddSingleton<ILeadRepository, InMemoryLeadRepository>();
        services.AddSingleton<IMemberRepository, InMemoryMemberRepository>();

        services.AddSingleton<ILanguageDetector, HeuristicLanguageDetector>();

        services.AddHttpClient<IWhatsAppMessageSender, WhatsAppCloudApiClient>();
        var openAiApiKey = configuration[$"{OpenAiOptions.SectionName}:ApiKey"];
        var provider = configuration["AiProvider"]?.Trim().ToLowerInvariant();

        provider ??= !string.IsNullOrWhiteSpace(configuration[$"{GeminiOptions.SectionName}:ApiKey"]) ? "gemini"
            : !string.IsNullOrWhiteSpace(configuration[$"{OpenAiOptions.SectionName}:ApiKey"]) ? "openai"
            : "azureopenai";

        switch (provider)
        {
            case "gemini":
                services.AddHttpClient<IAIAssistantService, GeminiAssistantService>();
                break;
            case "openai":
                services.AddHttpClient<IAIAssistantService, OpenAIAssistantService>();
                break;
            default:
                services.AddHttpClient<IAIAssistantService, AzureOpenAIAssistantService>();
                break;
        }

        services.AddScoped<ProcessIncomingMessageHandler>();

        return services;
    }
}