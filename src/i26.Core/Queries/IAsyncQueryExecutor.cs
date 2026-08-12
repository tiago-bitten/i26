using System.Linq.Expressions;

namespace i26.Core.Queries;

/// <summary>Awaits a query, whatever LINQ provider is behind it.</summary>
/// <remarks>What an application layer injects instead of referencing an ORM.</remarks>
public interface IAsyncQueryExecutor
{
    /// <summary>Runs a terminal LINQ operator over the query.</summary>
    /// <remarks>
    /// A query operator, not a materialization: <c>q =&gt; q.Count()</c> works,
    /// <c>q =&gt; q.ToList()</c> is <see cref="ToListAsync{T}"/>.
    /// </remarks>
    Task<TResult> ExecuteAsync<T, TResult>(
        IQueryable<T> query,
        Expression<Func<IQueryable<T>, TResult>> terminal,
        CancellationToken cancellationToken = default);

    /// <summary>Reads every row the query returns.</summary>
    Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);
}
