using GymChatAI.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymChatAI.Infrastructure.Persistence.EfCore.Configurations;

internal static class ModelBuilderExtensions
{
    public static EntityTypeBuilder<TEntity> ConfigureEntityBase<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : Entity
    {
        builder.HasKey(e => e.Id);

        // Ids are generated in the domain layer (Guid.NewGuid() in the entity constructor),
        // not by the database - so EF Core must never try to generate/overwrite them.
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc);

        return builder;
    }
}
