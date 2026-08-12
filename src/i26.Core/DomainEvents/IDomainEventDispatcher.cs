namespace i26.Core.DomainEvents;

/// <summary>Takes domain events to their handlers.</summary>
/// <remarks>
/// <c>AddDomainEvents</c> in i26.Cqrs registers one that runs the handlers in process. Register your
/// own to hand them to a queue or an outbox instead.
/// </remarks>
public interface IDomainEventDispatcher
{
    /// <summary>Dispatches the events, in the order they were raised.</summary>
    Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
