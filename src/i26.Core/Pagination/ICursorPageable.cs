namespace i26.Core.Pagination;

/// <summary>
/// What a row has to expose to be paged by cursor: the instant it was created, and an id to break
/// ties between rows created in the same instant.
/// </summary>
/// <typeparam name="TId">
/// The tie-breaker's type — a <see cref="Guid"/>, or the typed id the row already carries.
/// </typeparam>
/// <remarks>
/// The pair is what makes the page boundary exact. Ordering by the timestamp alone leaves rows
/// sharing one in arbitrary order, so a page could repeat a row or skip it — the id settles it.
/// Comparable because the keyset predicate orders by it, parsable because it travels in the cursor.
/// </remarks>
public interface ICursorPageable<TId>
    where TId : IComparable<TId>, IParsable<TId>
{
    /// <summary>Tie-breaker, unique across the rows being paged.</summary>
    TId Id { get; }

    /// <summary>What the page is ordered by, newest first.</summary>
    DateTimeOffset CreatedAt { get; }
}

/// <summary>A row whose tie-breaker is a raw <see cref="Guid"/>.</summary>
/// <remarks>
/// The shape for a row that has no typed id — a read model, a projection, a table this service does
/// not own. Everywhere else, name the id type: <c>ICursorPageable&lt;CourseId&gt;</c>.
/// </remarks>
public interface ICursorPageable : ICursorPageable<Guid>
{
}
