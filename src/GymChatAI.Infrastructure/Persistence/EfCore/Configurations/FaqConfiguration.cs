using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class FaqConfiguration : IEntityTypeConfiguration<Faq>
{
    public void Configure(EntityTypeBuilder<Faq> builder)
    {
        builder.ToTable("Faqs");
        builder.ConfigureEntityBase();

        builder.Property(f => f.Question).IsRequired().HasMaxLength(500);
        builder.Property(f => f.Answer).IsRequired().HasMaxLength(2000);
        builder.Property(f => f.Category).HasMaxLength(100);
        builder.Property(f => f.IsActive).IsRequired();

        builder.HasIndex(f => new { f.GymId, f.IsActive });

        builder.HasOne<Gym>().WithMany().HasForeignKey(f => f.GymId).OnDelete(DeleteBehavior.NoAction);
    }
}
