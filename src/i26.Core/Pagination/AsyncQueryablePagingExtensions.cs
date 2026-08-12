using i26.Core.Queries;
using i26.Core.Results;

namespace i26.Core.Pagination;

/// <summary>Cursor paging over an <see cref="IQueryable{T}"/>, awaited through an executor.</summary>
/// <remarks>The overload for a layer that pages without referencing an ORM.</remarks>
public static class AsyncQueryablePagingExtensions
{
    /// <summary>Reads one page, newest first, from a query that arrives filtered but not ordered.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="query">The filtered, unordered query.</param>
    /// <param name="executor">What awaits the two queries this runs.</param>
    /// <param name="request">What the caller asked for.</param>
    /// <param name="maxLimit">Ceiling on the page size, however many rows were asked for.</param>
    /// <param name="cancellationToken">Cancels the two queries this runs.</param>
    /// <returns>
    /// The page, or <see cref="PaginationErrors.InvalidCursor"/> when the cursor cannot be read.
    /// </returns>
    public static Task<Result<PagedResponse<T>>> ToPagedResponseAsync<T>(
        this IQueryable<T> query,
        IAsyncQueryExecutor executor,
        CursorPageRequest request,
        int maxLimit = CursorPageRequest.DefaultMaxLimit,
        CancellationToken cancellationToken = default)
        where T : ICursorPageable<Guid>
        => query.ToPagedResponseAsync<T, Guid>(executor, request, maxLimit, cancellationToken);

    /// <summary>Reads one page, newest first, from a query that arrives filtered but not ordered.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <typeparam name="TId">The tie-breaker's type.</typeparam>
    /// <param name="query">The filtered, unordered query.</param>
    /// <param name="executor">What awaits the two queries this runs.</param>
    /// <param name="request">What the caller asked for.</param>
    /// <param name="maxLimit">Ceiling on the page size, however many rows were asked for.</param>
    /// <param name="cancellationToken">Cancels the two queries this runs.</param>
    /// <returns>
    /// The page, or <see cref="PaginationErrors.InvalidCursor"/> when the cursor cannot be read.
    /// </returns>
    /// <remarks>
    /// Project with an object initializer, not a constructor, or the ordering has nothing to bind
    /// to. The table wants an index on <c>(CreatedAt DESC, Id DESC)</c> for this to be a seek.
    /// </remarks>
    public static async Task<Result<PagedResponse<T>>> ToPagedResponseAsync<T, TId>(
        this IQueryable<T> query,
        IAsyncQueryExecutor executor,
        CursorPageRequest request,
        int maxLimit = CursorPageRequest.DefaultMaxLimit,
        CancellationToken cancellationToken = default)
        where T : ICursorPageable<TId>
        where TId : IComparable<TId>, IParsable<TId>
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(request);

        var page = request.Normalize(maxLimit);

        DateTimeOffset cursorCreatedAt = default;
        TId cursorId = default!;

        if (!string.IsNullOrEmpty(page.Cursor) &&
            !Cursor.TryDecode(page.Cursor, out cursorCreatedAt, out cursorId))
        {
            return PaginationErrors.InvalidCursor;
        }

        // Counted before the cursor narrows anything: the total is of the whole matching set, not
        // of what is left after this page.
        int? total = page.IncludeTotal
            ? await executor.CountAsync(query, cancellationToken).ConfigureAwait(false)
            : null;

        if (!string.IsNullOrEmpty(page.Cursor))
        {
            query = query.Where(CursorPredicate<T, TId>.After(cursorCreatedAt, cursorId));
        }

        // One row more than asked for: its presence is the answer to "is there a next page".
        var items = await executor
            .ToListAsync(
                query
                    .OrderByDescending(CursorPredicate<T, TId>.CreatedAtSelector)
                    .ThenByDescending(CursorPredicate<T, TId>.IdSelector)
                    .Take(page.Limit + 1),
                cancellationToken)
            .ConfigureAwait(false);

        return CursorPage.From<T, TId>(items, page.Limit, total);
    }
}
