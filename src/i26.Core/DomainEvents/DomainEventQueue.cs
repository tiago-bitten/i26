namespace i26.Core.DomainEvents;

/// <summary>The domain events taken off the entities, waiting to be published.</summary>
/// <remarks>
/// One queue per unit of work — registered scoped. Collecting and publishing are separate steps on
/// purpose: something takes the events off the entities as they are saved, and whoever owns the
/// transaction publishes them once it has committed.
/// </remarks>
public sealed class DomainEventQueue
{
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly List<IDomainEvent> _pending = [];
    private readonly object _gate = new();

    /// <summary>Creates a queue that publishes through <paramref name="dispatcher"/>.</summary>
    public DomainEventQueue(IDomainEventDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        _dispatcher = dispatcher;
    }

    /// <summary>What has been collected and not yet published, in the order it was raised.</summary>
    public IReadOnlyList<IDomainEvent> Pending
    {
        get
        {
            lock (_gate)
            {
                return [.. _pending];
            }
        }
    }

    /// <summary>Adds events to the queue.</summary>
    /// <remarks>
    /// The events are copied as they arrive, so the caller is free to clear the list it handed over
    /// — which is exactly what taking them off an entity does.
    /// </remarks>
    public void Enqueue(IEnumerable<IDomainEvent> domainEvents)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        // Enumerated outside the lock: what arrives is someone else's collection, and running
        // unknown code while holding the lock is how a queue stops being reentrant.
        IDomainEvent[] arriving = [.. domainEvents];

        if (arriving.Length is 0)
        {
            return;
        }

        lock (_gate)
        {
            _pending.AddRange(arriving);
        }
    }

    /// <summary>Publishes everything queued, and everything the handlers queue in turn.</summary>
    /// <remarks>
    /// The queue is drained before each dispatch, so an event goes out once and a handler that saves
    /// further changes has its own events published by this same call — there is no second call to
    /// remember at the end of a handler.
    /// </remarks>
    public async Task PublishAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            IDomainEvent[] batch;

            lock (_gate)
            {
                if (_pending.Count is 0)
                {
                    return;
                }

                batch = [.. _pending];
                _pending.Clear();
            }

            await _dispatcher.DispatchAsync(batch, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Drops what is queued without publishing it.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _pending.Clear();
        }
    }
}
