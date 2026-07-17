using GymChatAI.Application.Abstractions;
using GymChatAI.Application.Loyalty;
using GymChatAI.Application.Messaging;
using GymChatAI.Infrastructure.AI;
using GymChatAI.Infrastructure.BackgroundServices;
using GymChatAI.Infrastructure.Identity;
using GymChatAI.Infrastructure.LanguageDetection;
using GymChatAI.Infrastructure.Options;
using GymChatAI.Infrastructure.Persistence;
using GymChatAI.Infrastructure.Persistence.EfCore;
using GymChatAI.Infrastructure.Persistence.EfCore.Repositories;
using GymChatAI.Infrastructure.WhatsApp;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GymChatAI.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers persistence, WhatsApp, AI, the loyalty engine, and the pending-AI-reply
    /// retry queue. Uses SQL Server (EF Core) when ConnectionStrings:GymChatDb is
    /// configured; otherwise falls back to the original in-memory store.
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
            services.AddScoped<ICampaignRepository, EfCampaignRepository>();
            services.AddScoped<ICampaignMessageRepository, EfCampaignMessageRepository>();
            services.AddScoped<IPendingAIReplyRepository, EfPendingAIReplyRepository>();
        }
        else
        {
            services.AddSingleton<InMemoryDataStore>();
            services.AddSingleton<IGymRepository, InMemoryGymRepository>();
            services.AddSingleton<IConversationRepository, InMemoryConversationRepository>();
            services.AddSingleton<IFaqRepository, InMemoryFaqRepository>();
            services.AddSingleton<ILeadRepository, InMemoryLeadRepository>();
            services.AddSingleton<IMemberRepository, InMemoryMemberRepository>();
            services.AddSingleton<ICampaignRepository, InMemoryCampaignRepository>();
            services.AddSingleton<ICampaignMessageRepository, InMemoryCampaignMessageRepository>();
            services.AddSingleton<InMemoryPendingAIReplyStore>();
            services.AddSingleton<IPendingAIReplyRepository, InMemoryPendingAIReplyRepository>();
        }

        services.AddScoped<LoyaltyEngineHandler>();
        services.AddHostedService<LoyaltyEngineBackgroundService>();

        services.AddScoped<RetryPendingAIRepliesHandler>();
        services.AddHostedService<PendingAIReplyBackgroundService>();

        return services;
    }

    /// <summary>
    /// Registers ASP.NET Core Identity (operator/admin accounts for the Administration Portal)
    /// with the built-in Bearer token scheme. Requires SQL Server.
    /// </summary>
    public static IServiceCollection AddGymChatIdentity(this IServiceCollection services)
    {
        services
            .AddIdentityApiEndpoints<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddClaimsPrincipalFactory<GymClaimsPrincipalFactory>()
            .AddEntityFrameworkStores<GymChatDbContext>();

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(Roles.Admin, Roles.PlatformAdmin))
            .AddPolicy(Policies.PlatformAdmin, policy => policy.RequireRole(Roles.PlatformAdmin));

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

        // Three interchangeable implementations of the same port (IAIAssistantService).
        // The top-level "AiProvider" setting picks explicitly; falls back to auto-detecting
        // from whichever *Options:ApiKey is filled in (Gemini > OpenAI > Azure) otherwise.
        var provider = configuration["AiProvider"]?.Trim().ToLowerInvariant();

        provider ??= !string.IsNullOrWhiteSpace(configuration[$"{GeminiOptions.SectionName}:ApiKey"]) ? "gemini"
            : !string.IsNullOrWhiteSpace(configuration[$"{OpenAIOptions.SectionName}:ApiKey"]) ? "openai"
            : "azureopenai";

        switch (provider)
        {
            case "gemini":
                services.AddHttpClient<IAIAssistantService, GeminiAIAssistantService>();
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
