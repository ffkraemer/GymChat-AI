using GymChatAI.Application.Abstractions;
using GymChatAI.Application.Compliance;
using GymChatAI.Application.Flows;
using GymChatAI.Application.Loyalty;
using GymChatAI.Application.Messaging;
using GymChatAI.Application.Templates;
using GymChatAI.Infrastructure.AI;
using GymChatAI.Infrastructure.BackgroundServices;
using GymChatAI.Infrastructure.Identity;
using GymChatAI.Infrastructure.LanguageDetection;
using GymChatAI.Infrastructure.Options;
using GymChatAI.Infrastructure.Persistence;
using GymChatAI.Infrastructure.Persistence.EfCore;
using GymChatAI.Infrastructure.Persistence.EfCore.Repositories;
using GymChatAI.Infrastructure.WhatsApp;
using GymChatAI.Infrastructure.WhatsApp.Flows;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GymChatAI.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
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

    /// <summary>
    /// Registers persistence, WhatsApp, AI, the loyalty engine, the pending-AI-reply retry
    /// queue, and the compliance dashboard. Uses SQL Server (EF Core) when
    /// ConnectionStrings:GymChatDb is configured; otherwise falls back to the original
    /// in-memory store.
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
            services.AddScoped<IClassTypeRepository, EfClassTypeRepository>();
            services.AddScoped<INotificationPreferenceRepository, EfNotificationPreferenceRepository>();
            services.AddScoped<IWhatsAppApiErrorRepository, EfWhatsAppApiErrorRepository>();
            services.AddScoped<IWhatsAppDeliveryFailureRepository, EfWhatsAppDeliveryFailureRepository>();
            services.AddScoped<IWhatsAppMessageTemplateRepository, EfWhatsAppMessageTemplateRepository>();
            services.AddScoped<IWhatsAppFlowRepository, EfWhatsAppFlowRepository>();
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
            services.AddSingleton<InMemoryClassTypeStore>();
            services.AddSingleton<IClassTypeRepository, InMemoryClassTypeRepository>();
            services.AddSingleton<InMemoryNotificationPreferenceStore>();
            services.AddSingleton<INotificationPreferenceRepository, InMemoryNotificationPreferenceRepository>();
            services.AddSingleton<InMemoryWhatsAppApiErrorStore>();
            services.AddSingleton<IWhatsAppApiErrorRepository, InMemoryWhatsAppApiErrorRepository>();
            services.AddSingleton<InMemoryWhatsAppDeliveryFailureStore>();
            services.AddSingleton<IWhatsAppDeliveryFailureRepository, InMemoryWhatsAppDeliveryFailureRepository>();
            services.AddSingleton<InMemoryWhatsAppMessageTemplateStore>();
            services.AddSingleton<IWhatsAppMessageTemplateRepository, InMemoryWhatsAppMessageTemplateRepository>();
            services.AddSingleton<InMemoryWhatsAppFlowStore>();
            services.AddSingleton<IWhatsAppFlowRepository, InMemoryWhatsAppFlowRepository>();
        }

        services.AddScoped<LoyaltyEngineHandler>();
        services.AddHostedService<LoyaltyEngineBackgroundService>();

        services.AddScoped<RetryPendingAIRepliesHandler>();
        services.AddHostedService<PendingAIReplyBackgroundService>();

        services.AddScoped<OnboardingFlowHandler>();

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
        services.AddHttpClient<IWhatsAppComplianceClient, WhatsAppComplianceClient>();
        services.AddScoped<ComplianceDashboardHandler>();

        services.AddHttpClient<IWhatsAppTemplateManagementClient, WhatsAppTemplateManagementClient>();
        services.AddScoped<WhatsAppTemplateHandler>();

        services.AddHttpClient<IWhatsAppWabaAdminClient, WhatsAppWabaAdminClient>();

        services.Configure<WhatsAppFlowOptions>(configuration.GetSection(WhatsAppFlowOptions.SectionName));
        services.AddSingleton<WhatsAppFlowEncryptionService>();
        services.AddSingleton<IWhatsAppFlowTokenStore, InMemoryWhatsAppFlowTokenStore>();
        services.AddHttpClient<IWhatsAppFlowManagementClient, WhatsAppFlowManagementClient>();
        services.AddScoped<WhatsAppFlowHandler>();
        services.AddScoped<WhatsAppFlowDataExchangeHandler>();
        services.AddScoped<WhatsAppFlowCompletionHandler>();

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