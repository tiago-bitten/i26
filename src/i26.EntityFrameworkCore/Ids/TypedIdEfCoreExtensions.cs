using System.Reflection;
using i26.Core.Ids;
using Microsoft.EntityFrameworkCore;

namespace i26.EntityFrameworkCore.Ids;

/// <summary>
/// Model conventions for the i26 typed identifiers.
/// </summary>
public static class TypedIdEfCoreExtensions
{
    /// <summary>Column type used to persist the ids.</summary>
    private const string ColumnType = "text";

    /// <summary>
    /// Column collation. <c>"C"</c> is Postgres' binary collation: it sorts byte by byte, which
    /// keeps the ids in chronological order and lets a B-tree index serve equality and prefix
    /// comparisons without depending on the server locale.
    /// </summary>
    private const string Collation = "C";

    /// <summary>
    /// Registers the converter, the comparer and the column mapping for every type implementing
    /// <see cref="ITypedId{TSelf}"/> in the given assemblies.
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
    /// </remarks>
    public static ModelConfigurationBuilder ApplyTypedIdConventions(
        this ModelConfigurationBuilder builder,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            foreach (var type in assembly.GetTypes())
            {
                if (!IsTypedId(type))
                {
                    continue;
                }

                builder
                    .Properties(type)
                    .HaveConversion(
                        typeof(TypedIdToStringConverter<>).MakeGenericType(type),
                        typeof(TypedIdComparer<>).MakeGenericType(type))
                    .HaveColumnType(ColumnType)
                    .UseCollation(Collation);
            }
        }

        return builder;
    }

    /// <summary>
    /// Tells whether the type is a concrete struct implementing <see cref="ITypedId{TSelf}"/> with
    /// itself as the generic argument.
    /// </summary>
    /// <param name="type">The candidate type.</param>
    /// <returns><see langword="true"/> when it is a typed id.</returns>
    public static bool IsTypedId(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!type.IsValueType || type.ContainsGenericParameters)
        {
            return false;
        }

        foreach (var candidate in type.GetInterfaces())
        {
            if (candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(ITypedId<>) &&
                candidate.GenericTypeArguments[0] == type)
            {
                return true;
            }
        }

        return false;
    }
}
