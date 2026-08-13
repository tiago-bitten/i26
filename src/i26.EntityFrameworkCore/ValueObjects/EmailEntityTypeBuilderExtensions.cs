using System.Linq.Expressions;
using i26.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace i26.EntityFrameworkCore.ValueObjects;

/// <summary>Mapping an <see cref="Email"/> from the configuration of the entity holding it.</summary>
public static class EmailEntityTypeBuilderExtensions
{
    /// <summary>Maps the property as an address: converter, comparer and column width.</summary>
    /// <typeparam name="TEntity">The entity holding it.</typeparam>
    /// <param name="builder">The entity being configured.</param>
    /// <param name="property">The address.</param>
    /// <param name="unique">Adds a unique index over it.</param>
    /// <returns>The property, so the configuration can go on saying things about it.</returns>
    /// <remarks>
    /// <para>
    /// <c>ApplyValueObjectConventions</c> already maps every <see cref="Email"/> in the model, so
    /// this is for a configuration that would rather say it where the property is declared — and
    /// for the unique index, which is a decision about this entity rather than about the type.
    /// </para>
    /// <code>
    /// protected override void ConfigureEntity(EntityTypeBuilder&lt;User&gt; builder)
    ///     =&gt; builder.HasEmail(user =&gt; user.Email, unique: true).IsRequired();
    /// </code>
    /// </remarks>
    public static PropertyBuilder<Email?> HasEmail<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, Email?>> property,
        bool unique = false)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(property);

        var configured = builder
            .Property(property)
            .HasConversion<EmailConverter, EmailComparer>()
            .HasMaxLength(Email.MaxLength);

        if (unique)
        {
            // By name, so the index is over the same property the expression just named without
            // rewriting the expression into the shape HasIndex wants.
            builder.HasIndex(configured.Metadata.Name).IsUnique();
        }

        return configured;
    }
}
