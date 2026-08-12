namespace i26.Core.DomainEvents;

/// <summary>Handles a domain event.</summary>
/// <typeparam name="TDomainEvent">The event it handles.</typeparam>
/// <remarks>Any number of handlers per event; one that throws stops the ones behind it.</remarks>
public interface IDomainEventHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    /// <summary>Reacts to the event.</summary>
    Task HandleAsync(TDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
