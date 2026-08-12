using i26.Core.Results;

namespace i26.Cqrs;

/// <summary>Handles a query.</summary>
/// <typeparam name="TQuery">The query it handles.</typeparam>
/// <typeparam name="TResponse">What the query answers with.</typeparam>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    /// <summary>Answers the query.</summary>
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
