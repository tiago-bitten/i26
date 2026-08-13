using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using i26.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace i26.EntityFrameworkCore.ValueObjects;

/// <summary>Model conventions for value objects.</summary>
public static class ValueObjectModelExtensions
{
    /// <summary>
    /// Maps every <see cref="IStringValueObject{TSelf}"/> in the given assemblies to its column,
    /// wherever it appears.
    /// </summary>
    /// <param name="builder">The model configuration builder.</param>
    /// <param name="assemblies">Assemblies to scan; typically the domain assembly.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    /// <remarks>
    /// The value objects of i26 are always included, so passing your own domain is enough:
    /// <code>
    /// protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    /// {
    ///     builder.ApplyTypedIdConventions(typeof(Course).Assembly);
    ///     builder.ApplyValueObjectConventions(typeof(Course).Assembly);
    /// }
    /// </code>
    /// A value object written in that assembly is mapped by the same call, without this library
    /// knowing it exists.
    /// </remarks>
    [RequiresUnreferencedCode("Scans the given assemblies for value objects, which trimming may have removed.")]
    [RequiresDynamicCode("Builds a converter and a comparer for each value object at runtime.")]
    public static ModelConfigurationBuilder ApplyValueObjectConventions(
        this ModelConfigurationBuilder builder,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var type in FindValueObjects(assemblies))
        {
            builder
                .Properties(type)
                .HaveConversion(
                    typeof(ValueObjectConverter<>).MakeGenericType(type),
                    typeof(ValueObjectComparer<>).MakeGenericType(type))
                .HaveMaxLength(MaxLengthOf(type));
        }

        return builder;
    }

    [RequiresUnreferencedCode("Scans the given assemblies for value objects, which trimming may have removed.")]
    private static IEnumerable<Type> FindValueObjects(Assembly[] assemblies)
    {
        // Ours are always in: a caller naming its own domain should not have to know that Email
        // lives somewhere else.
        foreach (var assembly in assemblies.Append(typeof(Email).Assembly).Distinct())
        {
            ArgumentNullException.ThrowIfNull(assembly);

            foreach (var type in assembly.GetTypes())
            {
                if (type is { IsClass: true, IsAbstract: false } && Implements(type))
                {
                    yield return type;
                }
            }
        }
    }

    // Closed over itself: a type implementing IStringValueObject<SomethingElse> is not one of these.
    private static bool Implements(Type type) =>
        Array.Exists(
            type.GetInterfaces(),
            candidate => candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == typeof(IStringValueObject<>)
                && candidate.GenericTypeArguments[0] == type);

    private static int MaxLengthOf(Type type) =>
        (int)type.GetProperty(
                nameof(IStringValueObject<Email>.MaxLength),
                BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;
}
