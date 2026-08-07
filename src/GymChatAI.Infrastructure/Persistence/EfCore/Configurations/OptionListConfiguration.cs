using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class OptionListConfiguration : IEntityTypeConfiguration<OptionList>
{
    public void Configure(EntityTypeBuilder<OptionList> builder)
    {
        builder.ToTable("OptionLists");
        builder.ConfigureEntityBase();

        builder.Property(l => l.Name).IsRequired().HasMaxLength(120);
        builder.Property(l => l.Key).IsRequired().HasMaxLength(80);
        builder.Property(l => l.IsSystem).IsRequired();
        builder.Property(l => l.IsActive).IsRequired();

        // Key is unique per scope (per gym, and once among globals where GymId is null).
        builder.HasIndex(l => new { l.GymId, l.Key }).IsUnique();

        builder.HasOne<Gym>().WithMany().HasForeignKey(l => l.GymId).OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(l => l.Items)
            .WithOne()
            .HasForeignKey(i => i.OptionListId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(l => l.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class OptionListItemConfiguration : IEntityTypeConfiguration<OptionListItem>
{
    public void Configure(EntityTypeBuilder<OptionListItem> builder)
    {
        builder.ToTable("OptionListItems");
        builder.ConfigureEntityBase();

        builder.Property(i => i.Value).IsRequired().HasMaxLength(120);
        builder.Property(i => i.Label).IsRequired().HasMaxLength(120);
        builder.Property(i => i.Order).IsRequired();

        builder.HasIndex(i => i.OptionListId);
    }
}
