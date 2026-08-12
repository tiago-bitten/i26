using i26.Core.Pagination;
using i26.Core.Results;
using i26.EntityFrameworkCore.Queries;

namespace i26.EntityFrameworkCore.Pagination;

/// <summary>Cursor paging over an <see cref="IQueryable{T}"/> that Entity Framework built.</summary>
/// <remarks>
/// The paging lives in i26.Core, over an <see cref="i26.Core.Queries.IAsyncQueryExecutor"/>; this
/// overload is for code that already has Entity Framework in front of it.
/// </remarks>
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
    /// Project with an object initializer: Entity Framework binds <c>new Row { CreatedAt = … }</c>
    /// back to its column and can order by it, and cannot do the same for <c>new Row(…)</c>.
    /// </remarks>
    public static Task<Result<PagedResponse<T>>> ToPagedResponseAsync<T, TId>(
        this IQueryable<T> query,
        CursorPageRequest request,
        int maxLimit = CursorPageRequest.DefaultMaxLimit,
        CancellationToken cancellationToken = default)
        where T : ICursorPageable<TId>
        where TId : IComparable<TId>, IParsable<TId>
        => query.ToPagedResponseAsync<T, TId>(
            EfCoreAsyncQueryBackend.Default, request, maxLimit, cancellationToken);
}
