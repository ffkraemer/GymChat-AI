using GymChatAI.Application.Abstractions;
using GymChatAI.Application.Messaging;
using GymChatAI.Infrastructure.Ai;
using GymChatAI.Infrastructure.AI;
using GymChatAI.Infrastructure.LanguageDetection;
using GymChatAI.Infrastructure.Options;
using GymChatAI.Infrastructure.Persistence;
using GymChatAI.Infrastructure.Persistence.EfCore;
using GymChatAI.Infrastructure.Persistence.EfCore.Repositories;
using GymChatAI.Infrastructure.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GymChatAI.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers persistence, WhatsApp and AI. Uses SQL Server (EF Core) when
    /// ConnectionStrings:GymChatDb is configured; otherwise falls back to the original
    /// in-memory store, so nothing changes for anyone who hasn't set up a database yet -
    /// this is a purely additive, opt-in upgrade path from the POC.
    /// </summary>
    public static IServiceCollection AddGymChatInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddSharedServices(services, configuration);

        var connectionString = configuration.GetConnectionString("GymChatDb");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<GymChatDbContext>(options =>
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

            services.AddScoped<IGymRepository, EfGymRepository>();
            services.AddScoped<IConversationRepository, EfConversationRepository>();
            services.AddScoped<IFaqRepository, EfFaqRepository>();
            services.AddScoped<ILeadRepository, EfLeadRepository>();
            services.AddScoped<IMemberRepository, EfMemberRepository>();
        }
        else
        {
            services.AddSingleton<InMemoryDataStore>();
            services.AddSingleton<IGymRepository, InMemoryGymRepository>();
            services.AddSingleton<IConversationRepository, InMemoryConversationRepository>();
            services.AddSingleton<IFaqRepository, InMemoryFaqRepository>();
            services.AddSingleton<ILeadRepository, InMemoryLeadRepository>();
            services.AddSingleton<IMemberRepository, InMemoryMemberRepository>();
        }

        return services;
    }

    private static void AddSharedServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.Configure<AzureOpenAIOptions>(configuration.GetSection(AzureOpenAIOptions.SectionName));
        services.Configure<OpenAIOptions>(configuration.GetSection(OpenAIOptions.SectionName));
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));

        services.AddSingleton<ILanguageDetector, HeuristicLanguageDetector>();

        services.AddHttpClient<IWhatsAppMessageSender, WhatsAppCloudApiClient>();

        // Three interchangeable implementations of the same port (IAiAssistantService).
        // The top-level "AiProvider" setting picks explicitly; falls back to auto-detecting
        // from whichever *Options:ApiKey is filled in (Gemini > OpenAI > Azure) otherwise.
        var provider = configuration["AiProvider"]?.Trim().ToLowerInvariant();

        provider ??= !string.IsNullOrWhiteSpace(configuration[$"{GeminiOptions.SectionName}:ApiKey"]) ? "gemini"
                   : !string.IsNullOrWhiteSpace(configuration[$"{OpenAIOptions.SectionName}:ApiKey"]) ? "openai"
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
    }
}