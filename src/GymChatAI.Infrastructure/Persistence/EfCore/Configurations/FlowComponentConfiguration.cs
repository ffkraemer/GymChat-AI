using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class FlowComponentConfiguration : IEntityTypeConfiguration<FlowComponent>
{
    public void Configure(EntityTypeBuilder<FlowComponent> builder)
    {
        builder.ToTable("FlowComponents");
        builder.ConfigureEntityBase();

        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.Order).IsRequired();
        builder.Property(c => c.Label).IsRequired().HasMaxLength(1000);
        builder.Property(c => c.VariableName).HasMaxLength(100);
        builder.Property(c => c.OptionsSource).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.StaticOptionsJson).HasColumnType("nvarchar(max)");
        builder.Property(c => c.FooterAction).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.FooterNextScreenId).HasMaxLength(100);
        builder.Property(c => c.FooterButtonLabel).HasMaxLength(100);
    }
}
