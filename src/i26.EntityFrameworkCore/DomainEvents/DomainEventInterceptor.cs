using i26.Core.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace i26.EntityFrameworkCore.DomainEvents;

/// <summary>
/// Takes the domain events off the tracked entities as they are saved, and — unless publication was
/// left to the caller — publishes them once the save has succeeded.
/// </summary>
/// <remarks>
/// Scoped, alongside the <see cref="DomainEventQueue"/> it fills:
/// <c>options.UseDomainEvents(serviceProvider)</c> wires both. The synchronous <c>SaveChanges</c>
/// collects but never publishes — publication is asynchronous — so a synchronous save leaves the
/// events queued for the next publication.
/// </remarks>
public sealed class DomainEventInterceptor : SaveChangesInterceptor
{
    private readonly DomainEventQueue _queue;
    private readonly DomainEventPublishing _publishing;

    /// <summary>Creates an interceptor that fills <paramref name="queue"/>.</summary>
    /// <param name="queue">The queue of the unit of work this context belongs to.</param>
    /// <param name="publishing">When to publish. Defaults to right after a successful save.</param>
    public DomainEventInterceptor(
        DomainEventQueue queue,
        DomainEventPublishing publishing = DomainEventPublishing.AfterSaveChanges)
    {
        ArgumentNullException.ThrowIfNull(queue);

        _queue = queue;
        _publishing = publishing;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Collect(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Collect(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (ShouldPublish(eventData.Context))
        {
            await _queue.PublishAsync(cancellationToken).ConfigureAwait(false);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    // Before the save, not after: a deleted entity is gone from the change tracker by the time the
    // save completes, and its event would go with it. A save that then fails leaves the events
    // queued, which is right — the change is still pending on the context, and a later save that
    // succeeds is the one that persisted it.
    private void Collect(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<IHasDomainEvents>())
        {
            if (entry.Entity.DomainEvents.Count is 0)
            {
                continue;
            }

            _queue.Enqueue(entry.Entity.DomainEvents);
            entry.Entity.ClearDomainEvents();
        }
    }

    private bool ShouldPublish(DbContext? context) =>
        _publishing is DomainEventPublishing.AfterSaveChanges
        && context is not null
        && context.Database.CurrentTransaction is null;
}
