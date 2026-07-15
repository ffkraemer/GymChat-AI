using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");
        builder.ConfigureEntityBase();

        builder.Property(c => c.ContactPhoneNumber).IsRequired().HasMaxLength(32);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.PreferredLanguage).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.LastMessageAtUtc).IsRequired();

        builder.HasIndex(c => new { c.GymId, c.ContactPhoneNumber });

        builder.HasOne<Gym>().WithMany().HasForeignKey(c => c.GymId).OnDelete(DeleteBehavior.NoAction);

        // Conversation is the aggregate root for messages: they are only ever created/read
        // through it (see Conversation.AddInboundMessage/AddOutboundMessage), backed by the
        // private `_messages` field. Mapping the navigation to that field - instead of the
        // read-only `Messages` property - lets EF Core materialize/track it without needing
        // a public setter, keeping the aggregate's invariants intact.
        builder.HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Navigation(c => c.Messages).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
