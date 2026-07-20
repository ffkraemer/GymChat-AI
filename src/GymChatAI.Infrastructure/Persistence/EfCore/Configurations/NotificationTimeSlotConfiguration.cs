using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class NotificationTimeSlotConfiguration : IEntityTypeConfiguration<NotificationTimeSlot>
{
    public void Configure(EntityTypeBuilder<NotificationTimeSlot> builder)
    {
        builder.ToTable("NotificationTimeSlots");
        builder.ConfigureEntityBase();

        builder.Property(s => s.DayOfWeek).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.TimeWindow).HasConversion<string>().HasMaxLength(20);
    }
}
