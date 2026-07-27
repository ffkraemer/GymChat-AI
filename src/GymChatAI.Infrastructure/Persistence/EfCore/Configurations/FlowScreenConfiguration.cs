using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class FlowScreenConfiguration : IEntityTypeConfiguration<FlowScreen>
{
    public void Configure(EntityTypeBuilder<FlowScreen> builder)
    {
        builder.ToTable("FlowScreens");
        builder.ConfigureEntityBase();

        builder.Property(s => s.ScreenId).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Title).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Order).IsRequired();

        builder.HasMany(s => s.Components)
            .WithOne()
            .HasForeignKey(c => c.FlowScreenId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Components).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
