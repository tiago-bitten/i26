using i26.Core.Pagination;
using i26.Core.Results;
using Microsoft.EntityFrameworkCore;

namespace i26.EntityFrameworkCore.Pagination;

/// <summary>
/// Cursor paging over an <see cref="IQueryable{T}"/>.
/// </summary>
public static class QueryablePagingExtensions
{
    /// <summary>Reads one page, newest first.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="query">The query to page over, filtered but not ordered.</param>
    /// <param name="request">How many rows, and where the last page stopped.</param>
    /// <param name="maxLimit">Ceiling applied to the requested limit.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    /// The page, or <see cref="PaginationErrors.InvalidCursor"/> when the cursor cannot be read.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="query"/> or <paramref name="request"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The ordering is applied here — <c>CreatedAt DESC, Id DESC</c> — so the caller passes the
    /// query filtered and nothing else. Order it yourself and this overrides it.
    /// </para>
    /// <code>
    /// var page = await db.Courses
    ///     .Where(course => course.TenantId == tenantId)
    ///     .Select(course => new CourseItem(course.Id, course.Title, course.CreatedAt))
    ///     .ToPagedResponseAsync(request, cancellationToken: ct);
    /// </code>
    /// <para>
    /// For this to stay an index seek rather than a scan, the table wants an index on
    /// <c>(CreatedAt DESC, Id DESC)</c> — with the columns the query filters by in front of it.
    /// </para>
    /// <para>
    /// Project with an object initializer, as above, and not with a constructor: Entity Framework
    /// binds <c>new Row { CreatedAt = … }</c> back to the column it came from, but cannot do the
    /// same for <c>new Row(…)</c>, so ordering by it has no translation. A response shape that
    /// takes a constructor is built afterwards, with <see cref="PagedResponse{T}.Map{TOut}"/>.
    /// </para>
    /// </remarks>
    public static async Task<Result<PagedResponse<T>>> ToPagedResponseAsync<T>(
        this IQueryable<T> query,
        CursorPageRequest request,
        int maxLimit = CursorPageRequest.DefaultMaxLimit,
        CancellationToken cancellationToken = default)
        where T : ICursorPageable
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);

        var page = request.Normalize(maxLimit);

        DateTimeOffset cursorCreatedAt = default;
        Guid cursorId = default;

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
            query = query.Where(CursorPredicate<T>.After(cursorCreatedAt, cursorId));
        }

        // One row more than asked for: its presence is the answer to "is there a next page".
        var items = await query
            .OrderByDescending(CursorPredicate<T>.CreatedAtSelector)
            .ThenByDescending(CursorPredicate<T>.IdSelector)
            .Take(page.Limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return CursorPage.From(items, page.Limit, total);
    }
}
