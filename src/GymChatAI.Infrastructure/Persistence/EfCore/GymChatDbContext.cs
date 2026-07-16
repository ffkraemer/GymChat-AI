using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymChatAI.Infrastructure.Persistence.EfCore;

public class GymChatDbContext : DbContext
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GymChatDbContext).Assembly);
    }
}
