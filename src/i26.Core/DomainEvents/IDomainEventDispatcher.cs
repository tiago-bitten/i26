namespace i26.Core.DomainEvents;

/// <summary>Takes domain events to their handlers.</summary>
/// <remarks>
/// <c>AddDomainEvents</c> in i26.Cqrs registers one that runs the handlers in process, in the scope
/// that published. An application that would rather hand the events to a queue, an outbox or a
/// background job registers its own instead.
/// </remarks>
public interface IDomainEventDispatcher
{
    /// <summary>Dispatches the events, in the order they were raised.</summary>
    Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
