using System.Linq.Expressions;
using i26.Core.Pagination;

namespace i26.EntityFrameworkCore.Pagination;

/// <summary>
/// The ordering and the keyset predicate of a page, as expressions the provider can translate.
/// </summary>
/// <typeparam name="T">The row type.</typeparam>
/// <remarks>
/// <para>
/// Written by hand rather than as a C# lambda for one reason: in a method constrained to
/// <see cref="ICursorPageable"/>, <c>item =&gt; item.CreatedAt</c> compiles to a member access
/// through a cast to the interface. Entity Framework can see through that when the row is an
/// entity, but not when it is a projection — <c>((ICursorPageable)new CourseItem(…)).CreatedAt</c>
/// has no translation, and paging over a projection is the common case. Reaching for the property
/// on the concrete type removes the cast and the problem with it.
/// </para>
/// <para>
/// The cursor values are held in an object the expression closes over, which is what a captured
/// variable looks like to the provider — so they travel as SQL parameters instead of being burned
/// into the statement as literals, and the database gets one query plan for every page.
/// </para>
/// </remarks>
internal static class CursorPredicate<T>
    where T : ICursorPageable
{
    private static readonly ParameterExpression Row = Expression.Parameter(typeof(T), "item");

    private static readonly MemberExpression CreatedAt =
        Access(nameof(ICursorPageable.CreatedAt));

    private static readonly MemberExpression Id =
        Access(nameof(ICursorPageable.Id));

    /// <summary>What the page is ordered by.</summary>
    internal static Expression<Func<T, DateTimeOffset>> CreatedAtSelector { get; } =
        Expression.Lambda<Func<T, DateTimeOffset>>(CreatedAt, Row);

    /// <summary>The tie-breaker.</summary>
    internal static Expression<Func<T, Guid>> IdSelector { get; } =
        Expression.Lambda<Func<T, Guid>>(Id, Row);

    /// <summary>Rows that come after the given position in the page order.</summary>
    /// <param name="createdAt">The instant the last page stopped at.</param>
    /// <param name="id">The id the last page stopped at.</param>
    /// <returns>The predicate.</returns>
    internal static Expression<Func<T, bool>> After(DateTimeOffset createdAt, Guid id)
    {
        var bound = Expression.Constant(new Bound(createdAt, id));
        var boundCreatedAt = Expression.Property(bound, nameof(Bound.CreatedAt));
        var boundId = Expression.Property(bound, nameof(Bound.Id));

        // Older than the cursor, or the same instant and a lower id.
        var body = Expression.OrElse(
            Expression.LessThan(CreatedAt, boundCreatedAt),
            Expression.AndAlso(
                Expression.Equal(CreatedAt, boundCreatedAt),
                Expression.LessThan(Id, boundId)));

        return Expression.Lambda<Func<T, bool>>(body, Row);
    }

    /// <summary>
    /// Reads a property off the concrete row type, falling back to the interface for the rare type
    /// that implements it explicitly.
    /// </summary>
    private static MemberExpression Access(string name)
    {
        var property = typeof(T).GetProperty(name);

        return property is not null
            ? Expression.Property(Row, property)
            : Expression.Property(Expression.Convert(Row, typeof(ICursorPageable)), name);
    }

    private sealed record Bound(DateTimeOffset CreatedAt, Guid Id);
}
