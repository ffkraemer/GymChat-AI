using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class GymConfiguration : IEntityTypeConfiguration<Gym>
{
    public void Configure(EntityTypeBuilder<Gym> builder)
    {
        builder.ToTable("Gyms");
        builder.ConfigureEntityBase();

        builder.Property(g => g.Name).IsRequired().HasMaxLength(200);
        builder.Property(g => g.WhatsAppPhoneNumberId).IsRequired().HasMaxLength(64);
        builder.Property(g => g.WhatsAppDisplayPhoneNumber).HasMaxLength(32);
        builder.Property(g => g.DefaultLanguage).HasConversion<string>().HasMaxLength(20);
        builder.Property(g => g.IsActive).IsRequired();

        // A WhatsApp phone number can only ever belong to one gym - this is how inbound
        // webhooks get routed to the right tenant.
        builder.HasIndex(g => g.WhatsAppPhoneNumberId).IsUnique();
    }
}
