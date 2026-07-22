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

    public DbSet<Gym> Gyms => Set<Gym>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Faq> Faqs => Set<Faq>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignMessage> CampaignMessages => Set<CampaignMessage>();
    public DbSet<PendingAIReply> PendingAIReplies => Set<PendingAIReply>();
    public DbSet<ClassType> ClassTypes => Set<ClassType>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationTimeSlot> NotificationTimeSlots => Set<NotificationTimeSlot>();
    public DbSet<WhatsAppApiError> WhatsAppApiErrors => Set<WhatsAppApiError>();
    public DbSet<WhatsAppDeliveryFailure> WhatsAppDeliveryFailures => Set<WhatsAppDeliveryFailure>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Must run first: it configures the Identity tables (AspNetUsers, AspNetRoles, etc.).
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GymChatDbContext).Assembly);
    }
}
