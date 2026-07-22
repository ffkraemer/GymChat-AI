using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class WhatsAppApiErrorConfiguration : IEntityTypeConfiguration<WhatsAppApiError>
{
    public void Configure(EntityTypeBuilder<WhatsAppApiError> builder)
    {
        builder.ToTable("WhatsAppApiErrors");
        builder.ConfigureEntityBase();

        builder.Property(e => e.Endpoint).IsRequired().HasMaxLength(100);
        builder.Property(e => e.HttpStatusCode).IsRequired();
        builder.Property(e => e.ErrorCode).HasMaxLength(20);
        builder.Property(e => e.ErrorMessage).IsRequired().HasMaxLength(4000);

        // The compliance dashboard always queries "errors for this gym since some date".
        builder.HasIndex(e => new { e.GymId, e.CreatedAtUtc });

        builder.HasOne<Gym>().WithMany().HasForeignKey(e => e.GymId).OnDelete(DeleteBehavior.NoAction);
    }
}
