using System.Linq.Expressions;
using i26.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace i26.EntityFrameworkCore.Entities;

/// <summary>Wiring for the i26 base entities.</summary>
public static class EntityDbContextExtensions
{
    /// <summary>Stamps the timestamps of every <see cref="IEntity"/> this context saves.</summary>
    /// <param name="builder">The options being built.</param>
    /// <param name="time">Where "now" comes from. Defaults to <see cref="TimeProvider.System"/>.</param>
    public static DbContextOptionsBuilder UseEntityTimestamps(
        this DbContextOptionsBuilder builder,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddInterceptors(new EntityTimestampInterceptor(time));
    }

    /// <summary>Hides soft-deleted rows from every query over an <see cref="ISoftDeletable"/>.</summary>
    /// <param name="modelBuilder">The model being built.</param>
    /// <returns>The same <paramref name="modelBuilder"/>, for chaining.</returns>
    /// <remarks>
    /// Call it at the end of <c>OnModelCreating</c>, after the entity types exist — a filter is
    /// applied to what is in the model at that moment, and anything configured later is not in it.
    /// A query that means to see them says <c>IgnoreQueryFilters()</c>.
    /// </remarks>
    public static ModelBuilder ApplySoftDeleteFilter(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType) || entityType.BaseType is not null)
            {
                continue;
            }

            // Built on the concrete type rather than on the interface: a member access through a
            // cast has no translation over anything but an entity, and one filter reaching for
            // ISoftDeletable.IsDeleted would be exactly that.
            var row = Expression.Parameter(entityType.ClrType, "row");
            var isDeleted = Expression.Property(row, nameof(ISoftDeletable.IsDeleted));

            modelBuilder
                .Entity(entityType.ClrType)
                .HasQueryFilter(Expression.Lambda(Expression.Not(isDeleted), row));
        }

        return modelBuilder;
    }
}
