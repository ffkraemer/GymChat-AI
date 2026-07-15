using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("Plans");
        builder.ConfigureEntityBase();

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.MonthlyPrice).HasColumnType("decimal(10,2)");
        builder.Property(p => p.IsActive).IsRequired();

        builder.HasOne<Gym>().WithMany().HasForeignKey(p => p.GymId).OnDelete(DeleteBehavior.NoAction);
    }
}
