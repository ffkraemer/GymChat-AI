using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class CampaignMessageConfiguration : IEntityTypeConfiguration<CampaignMessage>
{
    public void Configure(EntityTypeBuilder<CampaignMessage> builder)
    {
        builder.ToTable("CampaignMessages");
        builder.ConfigureEntityBase();

        builder.Property(m => m.RecipientPhoneNumber).IsRequired().HasMaxLength(32);
        builder.Property(m => m.RenderedContent).IsRequired().HasMaxLength(2000);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.PeriodKey).HasMaxLength(50);
        builder.Property(m => m.SentAtUtc);

        // The idempotency guard (ICampaignMessageRepository.ExistsAsync) always filters
        // by exactly these three columns, so a composite index keeps that check fast.
        builder.HasIndex(m => new { m.CampaignId, m.MemberId, m.PeriodKey });

        builder.HasOne<Campaign>().WithMany().HasForeignKey(m => m.CampaignId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Member>().WithMany().HasForeignKey(m => m.MemberId).OnDelete(DeleteBehavior.NoAction);
    }
}
