using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Dapper;
using i26.Core.Ids;

namespace i26.Dapper.Ids;

/// <summary>
/// Teaches Dapper to read and write the typed ids of an application.
/// </summary>
public static class TypedIdDapperExtensions
{
    /// <summary>Registers a handler for every typed id found in the given assemblies.</summary>
    /// <param name="assemblies">Assemblies to scan; usually the one holding the domain.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="assemblies"/>, or one of them, is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Dapper keeps its handlers in static state, so this belongs at startup and runs once:
    /// </para>
    /// <code>
    /// TypedIdDapperExtensions.AddTypedIdHandlers(typeof(CourseId).Assembly);
    /// </code>
    /// <para>
    /// Without it, a query selecting an id column into a typed id property fails while
    /// materializing — Dapper has no conversion to fall back on. Entity Framework learns the same
    /// thing from <c>ApplyTypedIdConventions</c>, and both end up reading the same prefixed string.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode("Scans the given assemblies for typed ids, which trimming may have removed.")]
    [RequiresDynamicCode("Builds a handler for each id type at runtime.")]
    public static void AddTypedIdHandlers(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var idType in TypedId.FindTypedIds(assemblies))
        {
            var handler = (SqlMapper.ITypeHandler)Activator.CreateInstance(
                typeof(TypedIdTypeHandler<>).MakeGenericType(idType))!;

            SqlMapper.AddTypeHandler(idType, handler);
        }
    }
}
