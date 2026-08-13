using i26.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace i26.EntityFrameworkCore.ValueObjects;

/// <summary>Model conventions for the i26 value objects.</summary>
public static class ValueObjectModelExtensions
{
    /// <summary>Maps every i26 value object to its column, wherever it appears.</summary>
    /// <param name="builder">The model configuration builder.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    /// <remarks>
    /// Once, in <c>ConfigureConventions</c>, rather than per property:
    /// <code>
    /// protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    /// {
    ///     builder.ApplyTypedIdConventions(typeof(Course).Assembly);
    ///     builder.ApplyValueObjectConventions();
    /// }
    /// </code>
    /// </remarks>
    public static ModelConfigurationBuilder ApplyValueObjectConventions(this ModelConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .Properties<Email>()
            .HaveConversion<EmailConverter, EmailComparer>()
            .HaveMaxLength(Email.MaxLength);

        return builder;
    }
}
