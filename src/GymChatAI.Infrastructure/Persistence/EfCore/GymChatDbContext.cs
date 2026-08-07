using GymChatAI.Domain.Entities;
using GymChatAI.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore;

/// <summary>
/// Extends IdentityDbContext so operator/admin accounts for the Administration Portal
/// live in the same database as the rest of the platform's data, using Guid keys throughout
/// to match the rest of the domain model's Entity.Id type.
/// </summary>
public class GymChatDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public GymChatDbContext(DbContextOptions<GymChatDbContext> options) : base(options)
    {
    }

    public DbSet<CampaignMessage> CampaignMessages => Set<CampaignMessage>();

    public DbSet<Campaign> Campaigns => Set<Campaign>();

    public DbSet<ClassType> ClassTypes => Set<ClassType>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Faq> Faqs => Set<Faq>();

    public DbSet<FlowComponent> FlowComponents => Set<FlowComponent>();

    public DbSet<FlowScreen> FlowScreens => Set<FlowScreen>();

    public DbSet<Gym> Gyms => Set<Gym>();

    public DbSet<Lead> Leads => Set<Lead>();

    public DbSet<Member> Members => Set<Member>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    public DbSet<NotificationTimeSlot> NotificationTimeSlots => Set<NotificationTimeSlot>();

    public DbSet<OptionListItem> OptionListItems => Set<OptionListItem>();

    public DbSet<OptionList> OptionLists => Set<OptionList>();

    public DbSet<PendingAIReply> PendingAIReplies => Set<PendingAIReply>();

    public DbSet<Plan> Plans => Set<Plan>();

    public DbSet<Promotion> Promotions => Set<Promotion>();

    public DbSet<WhatsAppApiError> WhatsAppApiErrors => Set<WhatsAppApiError>();

    public DbSet<WhatsAppDeliveryFailure> WhatsAppDeliveryFailures => Set<WhatsAppDeliveryFailure>();

    public DbSet<WhatsAppFlow> WhatsAppFlows => Set<WhatsAppFlow>();

    public DbSet<WhatsAppMessageTemplate> WhatsAppMessageTemplates => Set<WhatsAppMessageTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Must run first: it configures the Identity tables (AspNetUsers, AspNetRoles, etc.).
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GymChatDbContext).Assembly);
    }
}