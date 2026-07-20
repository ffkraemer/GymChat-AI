using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences");
        builder.ConfigureEntityBase();

        builder.Property(p => p.ContactPhoneNumber).IsRequired().HasMaxLength(32);
        builder.Property(p => p.IsOnboarded).IsRequired();
        builder.Property(p => p.OptedIntoNotifications).IsRequired();

        // SelectedClassTypeIds is a plain List<Guid> - EF Core maps primitive collections
        // like this to a JSON column automatically, no HasConversion needed.
        builder.Property(p => p.SelectedClassTypeIds).HasColumnName("SelectedClassTypeIds");

        // One preference per contact per gym - this is what GetByContactAsync looks up by.
        builder.HasIndex(p => new { p.GymId, p.ContactPhoneNumber }).IsUnique();

        builder.HasOne<Gym>().WithMany().HasForeignKey(p => p.GymId).OnDelete(DeleteBehavior.NoAction);

        // Same aggregate pattern as Conversation.Messages: Slots is only ever mutated through
        // NotificationPreference's own methods (AddTimeSlot, ResetSelections), so EF Core
        // writes to the private backing field directly rather than through the read-only property.
        builder.HasMany(p => p.Slots)
               .WithOne()
               .HasForeignKey(s => s.NotificationPreferenceId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Slots).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
