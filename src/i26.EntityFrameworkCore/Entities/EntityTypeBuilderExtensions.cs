using i26.Core.Ids;
using i26.Core.Pagination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace i26.EntityFrameworkCore.Entities;

/// <summary>Mapping that only makes sense for a row this library knows the shape of.</summary>
public static class EntityTypeBuilderExtensions
{
    /// <summary>Adds the index a cursor page reads, on <c>(CreatedAt DESC, Id DESC)</c>.</summary>
    /// <typeparam name="TEntity">The row type.</typeparam>
    /// <typeparam name="TId">The tie-breaker's type.</typeparam>
    /// <param name="builder">The entity being configured.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    /// <remarks>
    /// Both columns and both directions, in that order: a page orders by the instant and breaks
    /// ties by the id, so an index on <c>(Id, CreatedAt)</c> — the pair the other way round — is one
    /// the planner cannot use for it.
    /// </remarks>
    public static EntityTypeBuilder<TEntity> HasCursorIndex<TEntity, TId>(
        this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, ICursorPageable<TId>
        where TId : struct, ITypedId<TId>
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .HasIndex(row => new { row.CreatedAt, row.Id })
            .IsDescending(true, true);

        return builder;
    }
}
