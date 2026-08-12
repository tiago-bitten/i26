namespace i26.EntityFrameworkCore.DomainEvents;

/// <summary>When a <see cref="DomainEventInterceptor"/> publishes what it collected.</summary>
public enum DomainEventPublishing
{
    /// <summary>Once the save has succeeded and no transaction is open on the context.</summary>
    /// <remarks>
    /// With a transaction open the events stay queued, for whoever began it to publish after
    /// committing.
    /// </remarks>
    AfterSaveChanges,

    /// <summary>Never — the events are collected, and the caller publishes them.</summary>
    Manual,
}
