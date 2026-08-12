namespace i26.EntityFrameworkCore.DomainEvents;

/// <summary>When a <see cref="DomainEventInterceptor"/> publishes what it collected.</summary>
public enum DomainEventPublishing
{
    /// <summary>
    /// Inside <c>SaveChangesAsync</c>, once it has succeeded and no transaction is open on the
    /// context.
    /// </summary>
    /// <remarks>
    /// With a transaction open the events stay queued: they describe rows that a rollback would
    /// still take back, and whoever began the transaction publishes them after committing.
    /// </remarks>
    AfterSaveChanges,

    /// <summary>Never — the events are collected, and the caller publishes them.</summary>
    Manual,
}
