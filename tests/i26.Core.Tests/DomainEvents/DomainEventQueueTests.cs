using i26.Core.DomainEvents;

namespace i26.Core.Tests.DomainEvents;

/// <summary>
/// The queue is the seam between collecting events and publishing them, so what it guarantees is
/// ordering, that an event goes out exactly once, and that a cascade still ends in one call.
/// </summary>
public sealed class DomainEventQueueTests
{
    private sealed record Raised(int Number) : IDomainEvent;

    private sealed class RecordingDispatcher : IDomainEventDispatcher
    {
        public List<IReadOnlyList<IDomainEvent>> Batches { get; } = [];

        public Func<IReadOnlyList<IDomainEvent>, Task>? WhileDispatching { get; set; }

        public IEnumerable<IDomainEvent> Dispatched => Batches.SelectMany(batch => batch);

        public Task DispatchAsync(
            IReadOnlyList<IDomainEvent> domainEvents,
            CancellationToken cancellationToken = default)
        {
            Batches.Add(domainEvents);

            return WhileDispatching?.Invoke(domainEvents) ?? Task.CompletedTask;
        }
    }

    [Fact]
    public async Task An_empty_queue_publishes_nothing()
    {
        var dispatcher = new RecordingDispatcher();
        var queue = new DomainEventQueue(dispatcher);

        await queue.PublishAsync();

        Assert.Empty(dispatcher.Batches);
    }

    [Fact]
    public async Task Events_go_out_in_the_order_they_were_queued()
    {
        var dispatcher = new RecordingDispatcher();
        var queue = new DomainEventQueue(dispatcher);

        queue.Enqueue([new Raised(1), new Raised(2)]);
        queue.Enqueue([new Raised(3)]);

        await queue.PublishAsync();

        Assert.Equal([new Raised(1), new Raised(2), new Raised(3)], dispatcher.Dispatched);
    }

    [Fact]
    public async Task Everything_queued_goes_out_in_one_batch()
    {
        var dispatcher = new RecordingDispatcher();
        var queue = new DomainEventQueue(dispatcher);

        queue.Enqueue([new Raised(1)]);
        queue.Enqueue([new Raised(2)]);

        await queue.PublishAsync();

        Assert.Single(dispatcher.Batches);
    }

    [Fact]
    public async Task Publishing_empties_the_queue()
    {
        var dispatcher = new RecordingDispatcher();
        var queue = new DomainEventQueue(dispatcher);

        queue.Enqueue([new Raised(1)]);

        await queue.PublishAsync();
        await queue.PublishAsync();

        Assert.Empty(queue.Pending);
        Assert.Single(dispatcher.Dispatched);
    }

    [Fact]
    public async Task What_a_handler_raises_while_publishing_goes_out_in_the_same_call()
    {
        var dispatcher = new RecordingDispatcher();
        var queue = new DomainEventQueue(dispatcher);
        var cascaded = false;

        dispatcher.WhileDispatching = _ =>
        {
            if (!cascaded)
            {
                // What a handler that saves further changes ends up doing, one level down.
                cascaded = true;
                queue.Enqueue([new Raised(2)]);
            }

            return Task.CompletedTask;
        };

        queue.Enqueue([new Raised(1)]);

        await queue.PublishAsync();

        Assert.Equal([new Raised(1), new Raised(2)], dispatcher.Dispatched);
        Assert.Equal(2, dispatcher.Batches.Count);
        Assert.Empty(queue.Pending);
    }

    [Fact]
    public void Pending_is_what_has_been_collected_and_not_yet_published()
    {
        var queue = new DomainEventQueue(new RecordingDispatcher());

        queue.Enqueue([new Raised(1)]);

        Assert.Equal([new Raised(1)], queue.Pending);
    }

    [Fact]
    public void Pending_is_a_copy_and_does_not_change_underneath_the_caller()
    {
        var queue = new DomainEventQueue(new RecordingDispatcher());
        queue.Enqueue([new Raised(1)]);

        var pending = queue.Pending;
        queue.Enqueue([new Raised(2)]);

        Assert.Single(pending);
    }

    [Fact]
    public async Task The_queue_copies_what_it_is_given()
    {
        var dispatcher = new RecordingDispatcher();
        var queue = new DomainEventQueue(dispatcher);

        // An entity hands over its own list and clears it, which is what the interceptor does.
        var raised = new List<IDomainEvent> { new Raised(1) };
        queue.Enqueue(raised);
        raised.Clear();

        await queue.PublishAsync();

        Assert.Equal([new Raised(1)], dispatcher.Dispatched);
    }

    [Fact]
    public async Task Clear_drops_what_is_queued()
    {
        var dispatcher = new RecordingDispatcher();
        var queue = new DomainEventQueue(dispatcher);

        queue.Enqueue([new Raised(1)]);
        queue.Clear();

        await queue.PublishAsync();

        Assert.Empty(dispatcher.Batches);
    }

    [Fact]
    public async Task A_dispatcher_that_throws_leaves_nothing_behind_to_publish_twice()
    {
        var dispatcher = new RecordingDispatcher
        {
            WhileDispatching = _ => throw new InvalidOperationException("handler failed"),
        };

        var queue = new DomainEventQueue(dispatcher);
        queue.Enqueue([new Raised(1)]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.PublishAsync());

        Assert.Empty(queue.Pending);
    }

    [Fact]
    public void It_refuses_a_null_argument()
    {
        Assert.Throws<ArgumentNullException>(() => new DomainEventQueue(null!));
        Assert.Throws<ArgumentNullException>(() => new DomainEventQueue(new RecordingDispatcher()).Enqueue(null!));
    }
}
