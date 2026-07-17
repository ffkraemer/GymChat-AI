using GymChatAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

public class PendingAIReplyConfiguration : IEntityTypeConfiguration<PendingAIReply>
{
    public void Configure(EntityTypeBuilder<PendingAIReply> builder)
    {
        builder.ToTable("PendingAIReplies");
        builder.ConfigureEntityBase();

        builder.Property(p => p.UserMessage).IsRequired().HasMaxLength(4000);
        builder.Property(p => p.Attempts).IsRequired();
        builder.Property(p => p.LastAttemptAtUtc);
        builder.Property(p => p.LastError).HasMaxLength(2000);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        // The background retry service always queries "give me everything still Pending",
        // so that's the index that matters here.
        builder.HasIndex(p => p.Status);

        builder.HasOne<Conversation>().WithMany().HasForeignKey(p => p.ConversationId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Gym>().WithMany().HasForeignKey(p => p.GymId).OnDelete(DeleteBehavior.NoAction);
    }
}
