using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class WhatsAppDeliveryFailureConfiguration : IEntityTypeConfiguration<WhatsAppDeliveryFailure>
{
    public void Configure(EntityTypeBuilder<WhatsAppDeliveryFailure> builder)
    {
        builder.ToTable("WhatsAppDeliveryFailures");
        builder.ConfigureEntityBase();

        builder.Property(f => f.WhatsAppMessageId).IsRequired().HasMaxLength(128);
        builder.Property(f => f.RecipientPhoneNumber).IsRequired().HasMaxLength(32);
        builder.Property(f => f.ErrorCode).HasMaxLength(20);
        builder.Property(f => f.ErrorMessage).IsRequired().HasMaxLength(2000);

        builder.HasIndex(f => new { f.GymId, f.CreatedAtUtc });

        builder.HasOne<Gym>().WithMany().HasForeignKey(f => f.GymId).OnDelete(DeleteBehavior.NoAction);
    }
}
