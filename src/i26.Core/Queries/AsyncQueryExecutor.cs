using System.Linq.Expressions;

namespace i26.Core.Queries;

/// <summary>Hands each query to the backend that can run it, and runs the rest synchronously.</summary>
/// <remarks>
/// The application's single <see cref="IAsyncQueryExecutor"/>, singleton. With no backend for a
/// query — a list in a unit test — the operator runs on the calling thread and still answers.
/// </remarks>
public sealed class AsyncQueryExecutor : IAsyncQueryExecutor
{
    private readonly IAsyncQueryBackend[] _backends;

    /// <summary>Creates an executor over the given backends, in the order they are tried.</summary>
    public AsyncQueryExecutor(IEnumerable<IAsyncQueryBackend> backends)
    {
        ArgumentNullException.ThrowIfNull(backends);

        _backends = [.. backends];
    }

    /// <inheritdoc />
    public Task<TResult> ExecuteAsync<T, TResult>(
        IQueryable<T> query,
        Expression<Func<IQueryable<T>, TResult>> terminal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(terminal);

        var backend = BackendFor(query);

        return backend is not null
            ? backend.ExecuteAsync(query, terminal, cancellationToken)
            : Task.FromResult(terminal.Compile()(query));
    }

    /// <inheritdoc />
    public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var backend = BackendFor(query);

        return backend is not null
            ? backend.ToListAsync(query, cancellationToken)
            : Task.FromResult(query.ToList());
    }

    private IAsyncQueryBackend? BackendFor(IQueryable query)
    {
        foreach (var backend in _backends)
        {
            if (backend.CanExecute(query))
            {
                return backend;
            }
        }

        return null;
    }
}
