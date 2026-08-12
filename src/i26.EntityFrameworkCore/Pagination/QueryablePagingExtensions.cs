using i26.Core.Pagination;
using i26.Core.Results;
using Microsoft.EntityFrameworkCore;

namespace i26.EntityFrameworkCore.Pagination;

/// <summary>Cursor paging over an <see cref="IQueryable{T}"/>.</summary>
public static class QueryablePagingExtensions
{
    /// <summary>Reads one page, newest first, from a query that arrives filtered but not ordered.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="query">The filtered, unordered query.</param>
    /// <param name="request">What the caller asked for.</param>
    /// <param name="maxLimit">Ceiling on the page size, however many rows were asked for.</param>
    /// <param name="cancellationToken">Cancels the two queries this runs.</param>
    /// <returns>
    /// The page, or <see cref="PaginationErrors.InvalidCursor"/> when the cursor cannot be read.
    /// </returns>
    public static Task<Result<PagedResponse<T>>> ToPagedResponseAsync<T>(
        this IQueryable<T> query,
        CursorPageRequest request,
        int maxLimit = CursorPageRequest.DefaultMaxLimit,
        CancellationToken cancellationToken = default)
        where T : ICursorPageable<Guid>
        => query.ToPagedResponseAsync<T, Guid>(request, maxLimit, cancellationToken);

    /// <summary>Reads one page, newest first, from a query that arrives filtered but not ordered.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <typeparam name="TId">The tie-breaker's type.</typeparam>
    /// <param name="query">The filtered, unordered query.</param>
    /// <param name="request">What the caller asked for.</param>
    /// <param name="maxLimit">Ceiling on the page size, however many rows were asked for.</param>
    /// <param name="cancellationToken">Cancels the two queries this runs.</param>
    /// <returns>
    /// The page, or <see cref="PaginationErrors.InvalidCursor"/> when the cursor cannot be read.
    /// </returns>
    /// <remarks>
    /// Project with an object initializer, not a constructor: Entity Framework binds
    /// <c>new Row { CreatedAt = … }</c> back to its column and can order by it, but cannot do the
    /// same for <c>new Row(…)</c>. Build a constructor shape afterwards with
    /// <see cref="PagedResponse{T}.Map{TOut}"/>. For this to stay an index seek, the table wants an
    /// index on <c>(CreatedAt DESC, Id DESC)</c> behind whatever the query filters by.
    /// </remarks>
    public static async Task<Result<PagedResponse<T>>> ToPagedResponseAsync<T, TId>(
        this IQueryable<T> query,
        CursorPageRequest request,
        int maxLimit = CursorPageRequest.DefaultMaxLimit,
        CancellationToken cancellationToken = default)
        where T : ICursorPageable<TId>
        where TId : IComparable<TId>, IParsable<TId>
    {
        ArgumentNullException.ThrowIfNull(query);
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
            ? await query.CountAsync(cancellationToken).ConfigureAwait(false)
            : null;

        if (!string.IsNullOrEmpty(page.Cursor))
        {
            query = query.Where(CursorPredicate<T, TId>.After(cursorCreatedAt, cursorId));
        }

        // One row more than asked for: its presence is the answer to "is there a next page".
        var items = await query
            .OrderByDescending(CursorPredicate<T, TId>.CreatedAtSelector)
            .ThenByDescending(CursorPredicate<T, TId>.IdSelector)
            .Take(page.Limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return CursorPage.From<T, TId>(items, page.Limit, total);
    }
}
