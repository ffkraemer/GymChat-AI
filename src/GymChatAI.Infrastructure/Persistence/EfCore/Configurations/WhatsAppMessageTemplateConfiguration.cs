using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class WhatsAppMessageTemplateConfiguration : IEntityTypeConfiguration<WhatsAppMessageTemplate>
{
    public void Configure(EntityTypeBuilder<WhatsAppMessageTemplate> builder)
    {
        builder.ToTable("WhatsAppMessageTemplates");
        builder.ConfigureEntityBase();

        builder.Property(t => t.Name).IsRequired().HasMaxLength(512);
        builder.Property(t => t.Language).IsRequired().HasMaxLength(10);
        builder.Property(t => t.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.BodyText).IsRequired().HasMaxLength(2000);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.MetaTemplateId).HasMaxLength(128);
        builder.Property(t => t.RejectionReason).HasMaxLength(2000);

        // Meta requires template names to be unique per WABA+language, so this is the natural
        // uniqueness boundary on our side too.
        builder.HasIndex(t => new { t.GymId, t.Name, t.Language }).IsUnique();

        builder.HasOne<Gym>().WithMany().HasForeignKey(t => t.GymId).OnDelete(DeleteBehavior.NoAction);
    }
}
