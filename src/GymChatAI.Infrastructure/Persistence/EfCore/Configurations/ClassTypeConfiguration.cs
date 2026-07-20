using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class ClassTypeConfiguration : IEntityTypeConfiguration<ClassType>
{
    public void Configure(EntityTypeBuilder<ClassType> builder)
    {
        builder.ToTable("ClassTypes");
        builder.ConfigureEntityBase();

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.IsActive).IsRequired();

        builder.HasIndex(c => new { c.GymId, c.IsActive });

        builder.HasOne<Gym>().WithMany().HasForeignKey(c => c.GymId).OnDelete(DeleteBehavior.NoAction);
    }
}
