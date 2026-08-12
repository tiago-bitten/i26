namespace i26.Core.Pagination;

/// <summary>
/// What a row has to expose to be paged by cursor: the instant it was created, and an id to break
/// ties between rows created in the same instant.
/// </summary>
/// <remarks>
/// The pair is what makes the page boundary exact. Ordering by the timestamp alone leaves rows
/// sharing one in arbitrary order, so a page could repeat a row or skip it — the id settles it.
/// </remarks>
public interface ICursorPageable
{
    /// <summary>Tie-breaker, unique across the rows being paged.</summary>
    Guid Id { get; }

    /// <summary>What the page is ordered by, newest first.</summary>
    DateTimeOffset CreatedAt { get; }
}
