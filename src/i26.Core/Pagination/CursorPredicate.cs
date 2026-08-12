using System.Linq.Expressions;

namespace i26.Core.Pagination;

/// <summary>
/// The ordering and the keyset predicate of a page, as expressions the provider can translate.
/// </summary>
/// <typeparam name="T">The row type.</typeparam>
/// <typeparam name="TId">The tie-breaker's type.</typeparam>
/// <remarks>
/// <para>
/// Written by hand rather than as a C# lambda for one reason: in a method constrained to
/// <see cref="ICursorPageable{TId}"/>, <c>item =&gt; item.CreatedAt</c> compiles to a member access
/// through a cast to the interface. Entity Framework can see through that when the row is an
/// entity, but not when it is a projection — <c>((ICursorPageable&lt;Guid&gt;)new CourseItem(…)).CreatedAt</c>
/// has no translation, and paging over a projection is the common case. Reaching for the property
/// on the concrete type removes the cast and the problem with it.
/// </para>
/// <para>
/// The cursor values are held in an object the expression closes over, which is what a captured
/// variable looks like to the provider — so they travel as SQL parameters instead of being burned
/// into the statement as literals, and the database gets one query plan for every page.
/// </para>
/// </remarks>
internal static class CursorPredicate<T, TId>
    where T : ICursorPageable<TId>
    where TId : IComparable<TId>, IParsable<TId>
{
    private static readonly ParameterExpression Row = Expression.Parameter(typeof(T), "item");

    private static readonly MemberExpression CreatedAt =
        Access(nameof(ICursorPageable<TId>.CreatedAt));

    private static readonly MemberExpression Id =
        Access(nameof(ICursorPageable<TId>.Id));

    /// <summary>What the page is ordered by.</summary>
    internal static Expression<Func<T, DateTimeOffset>> CreatedAtSelector { get; } =
        Expression.Lambda<Func<T, DateTimeOffset>>(CreatedAt, Row);

    /// <summary>The tie-breaker.</summary>
    internal static Expression<Func<T, TId>> IdSelector { get; } =
        Expression.Lambda<Func<T, TId>>(Id, Row);

    /// <summary>Rows that come after the given position in the page order.</summary>
    /// <param name="createdAt">The instant the last page stopped at.</param>
    /// <param name="id">The id the last page stopped at.</param>
    /// <returns>The predicate.</returns>
    /// <exception cref="NotSupportedException">
    /// <typeparamref name="TId"/> has no <c>&lt;</c> operator, so no keyset predicate can be built
    /// over it.
    /// </exception>
    internal static Expression<Func<T, bool>> After(DateTimeOffset createdAt, TId id)
    {
        var bound = Expression.Constant(new Bound(createdAt, id));
        var boundCreatedAt = Expression.Property(bound, nameof(Bound.CreatedAt));
        var boundId = Expression.Property(bound, nameof(Bound.Id));

        // Older than the cursor, or the same instant and a lower id.
        var body = Expression.OrElse(
            Expression.LessThan(CreatedAt, boundCreatedAt),
            Expression.AndAlso(
                Expression.Equal(CreatedAt, boundCreatedAt),
                LessThan(Id, boundId)));

        return Expression.Lambda<Func<T, bool>>(body, Row);
    }

    /// <summary>
    /// The <c>&lt;</c> of the id type, or the reason there is none.
    /// </summary>
    /// <remarks>
    /// <see cref="IComparable{T}"/> is what the interface can ask for, and it is not the operator —
    /// a translated keyset predicate needs the operator, because <c>CompareTo</c> has no SQL. A
    /// typed id written by the generator has both; one written by hand may not, and the message
    /// says so rather than leaving an InvalidOperationException out of the expression tree.
    /// </remarks>
    private static Expression LessThan(Expression left, Expression right)
    {
        try
        {
            return Expression.LessThan(left, right);
        }
        catch (InvalidOperationException exception)
        {
            throw new NotSupportedException(
                $"{typeof(TId).Name} has no '<' operator, so the keyset predicate that reads the next " +
                "page cannot be translated to SQL. Declare the comparison operators on it — the " +
                "[TypedId] generator writes them — or page by a type that has them.",
                exception);
        }
    }

    private sealed record Bound(DateTimeOffset CreatedAt, TId Id);

    /// <summary>
    /// Reads a property off the concrete row type, falling back to the interface for the rare type
    /// that implements it explicitly.
    /// </summary>
    private static MemberExpression Access(string name)
    {
        var property = typeof(T).GetProperty(name);

        return property is not null
            ? Expression.Property(Row, property)
            : Expression.Property(Expression.Convert(Row, typeof(ICursorPageable<TId>)), name);
    }
}
