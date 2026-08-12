namespace i26.Cqrs;

/// <summary>A request that reads and changes nothing.</summary>
/// <typeparam name="TResponse">What the query answers with.</typeparam>
/// <remarks>Handled by <see cref="IQueryHandler{TQuery, TResponse}"/>.</remarks>
public interface IQuery<TResponse>;
