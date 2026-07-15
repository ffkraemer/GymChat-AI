using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("Members");
        builder.ConfigureEntityBase();

        builder.Property(m => m.FullName).IsRequired().HasMaxLength(200);
        builder.Property(m => m.PhoneNumber).IsRequired().HasMaxLength(32);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.BirthDate);
        builder.Property(m => m.LastCheckInAtUtc);

        builder.HasIndex(m => new { m.GymId, m.PhoneNumber });

        builder.HasOne<Gym>().WithMany().HasForeignKey(m => m.GymId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Plan>().WithMany().HasForeignKey(m => m.PlanId).OnDelete(DeleteBehavior.NoAction);
    }
}
