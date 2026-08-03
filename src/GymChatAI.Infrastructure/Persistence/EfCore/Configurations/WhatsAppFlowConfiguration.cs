using GymChatAI.Domain.Entities;
using GymChatAI.Infrastructure.Persistence.EfCore.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class WhatsAppFlowConfiguration : IEntityTypeConfiguration<WhatsAppFlow>
{
    public void Configure(EntityTypeBuilder<WhatsAppFlow> builder)
    {
        builder.ToTable("WhatsAppFlows");
        builder.ConfigureEntityBase();

        builder.Property(f => f.Name).IsRequired().HasMaxLength(200);
        builder.Property(f => f.WhatsAppBusinessAccountId).HasMaxLength(64);
        builder.Property(f => f.MetaFlowId).HasMaxLength(128);
        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.FlowJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(f => f.IsDynamic).IsRequired();
        builder.Property(f => f.EndpointUri).HasMaxLength(500);

        builder.HasIndex(f => f.GymId);

        builder.HasOne<Gym>().WithMany().HasForeignKey(f => f.GymId).OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(f => f.Screens)
            .WithOne()
            .HasForeignKey(s => s.WhatsAppFlowId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(f => f.Screens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
