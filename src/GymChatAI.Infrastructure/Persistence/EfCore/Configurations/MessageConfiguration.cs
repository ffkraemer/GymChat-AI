using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");
        builder.ConfigureEntityBase();

        builder.Property(m => m.Content).IsRequired().HasMaxLength(4000);
        builder.Property(m => m.Direction).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Origin).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.DetectedLanguage).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.WhatsAppMessageId).HasMaxLength(128);

        // Used for idempotency checks (has this WhatsApp message already been processed?)
        // and for status-callback lookups.
        builder.HasIndex(m => m.WhatsAppMessageId);
    }
}
