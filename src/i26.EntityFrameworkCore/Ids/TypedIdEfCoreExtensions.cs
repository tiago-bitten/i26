using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using i26.Core.Ids;
using Microsoft.EntityFrameworkCore;

namespace i26.EntityFrameworkCore.Ids;

/// <summary>
/// Model conventions for the i26 typed identifiers.
/// </summary>
public static class TypedIdEfCoreExtensions
{
    /// <summary>
    /// Registers the converter, the comparer and a Postgres column mapping for every type
    /// implementing <see cref="ITypedId{TSelf}"/> in the given assemblies.
    /// </summary>
    /// <param name="builder">The model configuration builder.</param>
    /// <param name="assemblies">Assemblies to scan; typically the domain assembly.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> or <paramref name="assemblies"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// The reflection scan runs once, while the model is being built. Typical usage:
    /// <code>
    /// protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    ///     => builder.ApplyTypedIdConventions(typeof(User).Assembly);
    /// </code>
    /// On anything other than Postgres, pass <see cref="TypedIdStorage.ProviderDefault"/>.
    /// </remarks>
    [RequiresUnreferencedCode("Scans the given assemblies for typed ids, which trimming may have removed.")]
    [RequiresDynamicCode("Builds a converter and a comparer for each id type at runtime.")]
    public static ModelConfigurationBuilder ApplyTypedIdConventions(
        this ModelConfigurationBuilder builder,
        params Assembly[] assemblies)
        => builder.ApplyTypedIdConventions(TypedIdStorage.Postgres, assemblies);

    /// <summary>
    /// Registers the converter, the comparer and the column mapping for every type implementing
    /// <see cref="ITypedId{TSelf}"/> in the given assemblies.
    /// </summary>
    /// <param name="builder">The model configuration builder.</param>
    /// <param name="storage">How the column is declared.</param>
    /// <param name="assemblies">Assemblies to scan; typically the domain assembly.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/>, <paramref name="storage"/> or <paramref name="assemblies"/> is
    /// <see langword="null"/>.
    /// </exception>
    [RequiresUnreferencedCode("Scans the given assemblies for typed ids, which trimming may have removed.")]
    [RequiresDynamicCode("Builds a converter and a comparer for each id type at runtime.")]
    public static ModelConfigurationBuilder ApplyTypedIdConventions(
        this ModelConfigurationBuilder builder,
        TypedIdStorage storage,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var type in TypedId.FindTypedIds(assemblies))
        {
            var properties = builder
                .Properties(type)
                .HaveConversion(
                    typeof(TypedIdToStringConverter<>).MakeGenericType(type),
                    typeof(TypedIdComparer<>).MakeGenericType(type));

            if (storage.ColumnType is not null)
            {
                properties.HaveColumnType(storage.ColumnType);
            }

            if (storage.Collation is not null)
            {
                properties.UseCollation(storage.Collation);
            }
        }

        return builder;
    }
}
