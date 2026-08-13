using System.Linq.Expressions;
using i26.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace i26.EntityFrameworkCore.ValueObjects;

/// <summary>Mapping a value object from the configuration of the entity holding it.</summary>
public static class ValueObjectEntityTypeBuilderExtensions
{
    /// <summary>Maps the property as its value object: converter, comparer and column width.</summary>
    /// <typeparam name="TEntity">The entity holding it.</typeparam>
    /// <typeparam name="TValue">The value object.</typeparam>
    /// <param name="builder">The entity being configured.</param>
    /// <param name="property">The property.</param>
    /// <param name="unique">Adds a unique index over it.</param>
    /// <returns>The property, so the configuration can go on saying things about it.</returns>
    /// <remarks>
    /// <c>ApplyValueObjectConventions</c> already maps the type wherever it appears, so this is for
    /// a configuration that would rather say it where the property is declared — and for the unique
    /// index, which is a decision about this entity rather than about the type.
    /// <code>
    /// protected override void ConfigureEntity(EntityTypeBuilder&lt;User&gt; builder)
    ///     =&gt; builder.HasValueObject(user =&gt; user.Email, unique: true).IsRequired();
    /// </code>
    /// </remarks>
    public static PropertyBuilder<TValue?> HasValueObject<TEntity, TValue>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, TValue?>> property,
        bool unique = false)
        where TEntity : class
        where TValue : class, IStringValueObject<TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(property);

        var configured = builder
            .Property(property)
            .HasConversion<ValueObjectConverter<TValue>, ValueObjectComparer<TValue>>()
            .HasMaxLength(TValue.MaxLength);

        if (unique)
        {
            // By name, so the index is over the same property the expression just named without
            // rewriting the expression into the shape HasIndex wants.
            builder.HasIndex(configured.Metadata.Name).IsUnique();
        }

        return configured;
    }
}
