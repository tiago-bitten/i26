using System.Linq.Expressions;
using i26.Core.Specifications;

namespace i26.Core.Queries;

/// <summary>The terminal operators an application reaches for most, spelled out.</summary>
/// <remarks>
/// Each is <c>ExecuteAsync</c> with the operator written for you. Anything missing is the same call
/// with the operator you want: <c>ExecuteAsync(invoices, q =&gt; q.Sum(i =&gt; i.Amount), ct)</c>.
/// </remarks>
public static class AsyncQueryExecutorExtensions
{
    /// <summary>Reads every row the query returns, into an array.</summary>
    public static async Task<T[]> ToArrayAsync<T>(
        this IAsyncQueryExecutor executor,
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executor);

        return [.. await executor.ToListAsync(query, cancellationToken).ConfigureAwait(false)];
    }

    /// <summary>Counts the rows.</summary>
    public static Task<int> CountAsync<T>(
        this IAsyncQueryExecutor executor,
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executor);

        return executor.ExecuteAsync(query, rows => rows.Count(), cancellationToken);
    }

    /// <summary>Counts the rows that match.</summary>
    public static Task<int> CountAsync<T>(
        this IAsyncQueryExecutor executor,
        IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return executor.CountAsync(query.Where(predicate), cancellationToken);
    }

    /// <summary>Counts the rows, past what an <see cref="int"/> holds.</summary>
    public static Task<long> LongCountAsync<T>(
        this IAsyncQueryExecutor executor,
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executor);

        return executor.ExecuteAsync(query, rows => rows.LongCount(), cancellationToken);
    }

    /// <summary>Counts the rows that match, past what an <see cref="int"/> holds.</summary>
    public static Task<long> LongCountAsync<T>(
        this IAsyncQueryExecutor executor,
        IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return executor.LongCountAsync(query.Where(predicate), cancellationToken);
    }

    /// <summary>Whether the query returns anything at all.</summary>
    public static Task<bool> AnyAsync<T>(
        this IAsyncQueryExecutor executor,
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executor);

        return executor.ExecuteAsync(query, rows => rows.Any(), cancellationToken);
    }

    /// <summary>Whether any row matches.</summary>
    public static Task<bool> AnyAsync<T>(
        this IAsyncQueryExecutor executor,
        IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return executor.AnyAsync(query.Where(predicate), cancellationToken);
    }

    /// <summary>Whether every row matches.</summary>
    public static async Task<bool> AllAsync<T>(
        this IAsyncQueryExecutor executor,
        IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        // Asked as "nothing fails it": same question, same SQL, and the predicate stays a quoted
        // lambda instead of the captured expression that Queryable.All would have made of it.
        var fails = Predicates.Negate(predicate);

        return !await executor.AnyAsync(query.Where(fails), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The first row.</summary>
    public static Task<T> FirstAsync<T>(
        this IAsyncQueryExecutor executor,
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executor);

        return executor.ExecuteAsync(query, rows => rows.First(), cancellationToken);
    }

    /// <summary>The first row that matches.</summary>
    public static Task<T> FirstAsync<T>(
        this IAsyncQueryExecutor executor,
        IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return executor.FirstAsync(query.Where(predicate), cancellationToken);
    }

    /// <summary>The first row, or nothing.</summary>
    public static Task<T?> FirstOrDefaultAsync<T>(
        this IAsyncQueryExecutor executor,
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executor);

        return executor.ExecuteAsync(query, rows => rows.FirstOrDefault(), cancellationToken);
    }

    /// <summary>The first row that matches, or nothing.</summary>
    public static Task<T?> FirstOrDefaultAsync<T>(
        this IAsyncQueryExecutor executor,
        IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return executor.FirstOrDefaultAsync(query.Where(predicate), cancellationToken);
    }

    /// <summary>The one row the query returns.</summary>
    public static Task<T> SingleAsync<T>(
        this IAsyncQueryExecutor executor,
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executor);

        return executor.ExecuteAsync(query, rows => rows.Single(), cancellationToken);
    }

    /// <summary>The one row that matches.</summary>
    public static Task<T> SingleAsync<T>(
        this IAsyncQueryExecutor executor,
        IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return executor.SingleAsync(query.Where(predicate), cancellationToken);
    }

    /// <summary>The one row the query returns, or nothing.</summary>
    public static Task<T?> SingleOrDefaultAsync<T>(
        this IAsyncQueryExecutor executor,
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executor);

        return executor.ExecuteAsync(query, rows => rows.SingleOrDefault(), cancellationToken);
    }

    /// <summary>The one row that matches, or nothing.</summary>
    public static Task<T?> SingleOrDefaultAsync<T>(
        this IAsyncQueryExecutor executor,
        IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return executor.SingleOrDefaultAsync(query.Where(predicate), cancellationToken);
    }
}
