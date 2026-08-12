using System.Linq.Expressions;
using i26.Core.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace i26.EntityFrameworkCore.Queries;

/// <summary>Runs a query that Entity Framework built, asynchronously.</summary>
/// <remarks>Stateless, so one instance serves the application.</remarks>
public sealed class EfCoreAsyncQueryBackend : IAsyncQueryBackend
{
    /// <summary>The instance to use where there is no container to ask.</summary>
    public static EfCoreAsyncQueryBackend Default { get; } = new();

    /// <inheritdoc />
    public bool CanExecute(IQueryable query) => query?.Provider is IAsyncQueryProvider;

    /// <inheritdoc />
    public Task<TResult> ExecuteAsync<T, TResult>(
        IQueryable<T> query,
        Expression<Func<IQueryable<T>, TResult>> terminal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(terminal);

        var provider = AsyncProviderOf(query);

        // Inlining the operator over the query's own expression and handing it to the provider is
        // what CountAsync and every other extension here does internally, one operator at a time.
        var expression = Inline(terminal, query.Expression);

        return provider.ExecuteAsync<Task<TResult>>(expression, cancellationToken);
    }

    /// <inheritdoc />
    public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = AsyncProviderOf(query);

        return query.ToListAsync(cancellationToken);
    }

    private static IAsyncQueryProvider AsyncProviderOf<T>(IQueryable<T> query) =>
        query.Provider as IAsyncQueryProvider
        ?? throw new InvalidOperationException(
            $"This query is not one Entity Framework built — its provider is " +
            $"{query.Provider.GetType().Name}, which has no asynchronous side. Resolve " +
            $"{nameof(IAsyncQueryExecutor)} from the container instead of reaching for this backend: " +
            "it falls back to running the operator on the calling thread.");

    // The lambda arrives as q => q.Count(); the provider wants Queryable.Count(<the query>). Putting
    // the query's expression where the parameter was is the whole translation.
    private static Expression Inline<T, TResult>(
        Expression<Func<IQueryable<T>, TResult>> terminal,
        Expression query)
        => new ParameterReplacer(terminal.Parameters[0], query).Visit(terminal.Body);

    private sealed class ParameterReplacer(ParameterExpression parameter, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == parameter ? replacement : base.VisitParameter(node);
    }
}
