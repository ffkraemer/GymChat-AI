using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class WhatsAppFlowConfiguration : IEntityTypeConfiguration<WhatsAppFlow>
{
    public void Configure(EntityTypeBuilder<WhatsAppFlow> builder)
    {
        builder.ToTable("WhatsAppFlows");
        builder.ConfigureEntityBase();

        builder.Property(f => f.Name).IsRequired().HasMaxLength(200);
        builder.Property(f => f.MetaFlowId).HasMaxLength(128);
        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.FlowJson).IsRequired().HasColumnType("nvarchar(max)");

        builder.HasIndex(f => f.GymId);

        builder.HasOne<Gym>().WithMany().HasForeignKey(f => f.GymId).OnDelete(DeleteBehavior.NoAction);
    }
}
