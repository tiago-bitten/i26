namespace i26.Core.DomainEvents;

/// <summary>Handles a domain event.</summary>
/// <typeparam name="TDomainEvent">The event it handles.</typeparam>
/// <remarks>
/// An event may have any number of handlers, each resolved and run on its own. Unlike a command,
/// nothing answers back: a handler that fails throws, and stops the ones queued behind it.
/// </remarks>
public interface IDomainEventHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    /// <summary>Reacts to the event.</summary>
    Task HandleAsync(TDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
