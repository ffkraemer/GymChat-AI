using GymChatAI.Application.Abstractions;
using GymChatAI.Application.Messaging;
using GymChatAI.Infrastructure.AI;
using GymChatAI.Infrastructure.LanguageDetection;
using GymChatAI.Infrastructure.Options;
using GymChatAI.Infrastructure.Persistence;
using GymChatAI.Infrastructure.WhatsApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GymChatAI.Shared.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGymChatInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.Configure<AzureOpenAIOptions>(configuration.GetSection(AzureOpenAIOptions.SectionName));

        services.AddSingleton<InMemoryDataStore>();
        services.AddSingleton<IGymRepository, InMemoryGymRepository>();
        services.AddSingleton<IConversationRepository, InMemoryConversationRepository>();
        services.AddSingleton<IFaqRepository, InMemoryFaqRepository>();
        services.AddSingleton<ILeadRepository, InMemoryLeadRepository>();
        services.AddSingleton<IMemberRepository, InMemoryMemberRepository>();

        services.AddSingleton<ILanguageDetector, HeuristicLanguageDetector>();

        services.AddHttpClient<IWhatsAppMessageSender, WhatsAppCloudApiClient>();
        services.AddHttpClient<IAIAssistantService, AzureOpenAIAssistantService>();

        services.AddScoped<ProcessIncomingMessageHandler>();

        return services;
    }
}