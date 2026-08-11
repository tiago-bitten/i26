using i26.Core.Results;

namespace i26.Cqrs;

/// <summary>
/// Handles a query.
/// </summary>
/// <typeparam name="TQuery">The query it handles.</typeparam>
/// <typeparam name="TResponse">What the query answers with.</typeparam>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    /// <summary>Answers the query.</summary>
    /// <param name="query">What to read.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The answer, or the failure that stopped it.</returns>
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
