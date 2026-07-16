using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("Campaigns");
        builder.ConfigureEntityBase();

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.MessageTemplate).IsRequired().HasMaxLength(2000);
        builder.Property(c => c.TriggerDayOffset);
        builder.Property(c => c.IsActive).IsRequired();

        builder.HasIndex(c => new { c.GymId, c.Type, c.IsActive });

        builder.HasOne<Gym>().WithMany().HasForeignKey(c => c.GymId).OnDelete(DeleteBehavior.NoAction);
    }
}
