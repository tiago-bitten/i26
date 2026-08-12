using i26.Core.Entities;
using i26.Core.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace i26.EntityFrameworkCore.Entities;

/// <summary>The mapping every i26 entity shares, which is less than it looks.</summary>
/// <typeparam name="TEntity">The entity being configured.</typeparam>
/// <typeparam name="TId">Its id type.</typeparam>
/// <remarks>
/// Conventions already find the key, refuse to generate the id, make the timestamps required and
/// leave the domain events out of the model — all of it measured rather than assumed, in
/// <c>EntityConfigurationTests</c>. What is left is the index a cursor page reads, and the place to
/// put the mapping that is actually yours.
/// <code>
/// internal sealed class CourseConfiguration : EntityConfiguration&lt;Course, CourseId&gt;
/// {
///     protected override void ConfigureEntity(EntityTypeBuilder&lt;Course&gt; builder)
///         =&gt; builder.Property(course =&gt; course.Title).HasMaxLength(200);
/// }
/// </code>
/// A <see cref="DeletableEntity{TId}"/> uses this one too: hiding the deleted rows is a filter over
/// the whole model, applied once with <c>ApplySoftDeleteFilter</c>.
/// </remarks>
public abstract class EntityConfiguration<TEntity, TId> : IEntityTypeConfiguration<TEntity>
    where TEntity : Entity<TId>
    where TId : struct, ITypedId<TId>
{
    /// <summary>Whether this table is paged by cursor, and so wants the index for it.</summary>
    /// <remarks>Answer <see langword="false"/> for a table nobody pages: an index costs every write.</remarks>
    protected virtual bool IsPaged => true;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (IsPaged)
        {
            builder.HasCursorIndex<TEntity, TId>();
        }

        ConfigureEntity(builder);
    }

    /// <summary>The mapping this entity does not share with any other.</summary>
    protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);
}
