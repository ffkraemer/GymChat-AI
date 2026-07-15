using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");
        builder.ConfigureEntityBase();

        builder.Property(l => l.PhoneNumber).IsRequired().HasMaxLength(32);
        builder.Property(l => l.Name).HasMaxLength(200);
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.InterestNotes).HasMaxLength(1000);

        builder.HasIndex(l => new { l.GymId, l.PhoneNumber });

        builder.HasOne<Gym>().WithMany().HasForeignKey(l => l.GymId).OnDelete(DeleteBehavior.NoAction);
    }
}
