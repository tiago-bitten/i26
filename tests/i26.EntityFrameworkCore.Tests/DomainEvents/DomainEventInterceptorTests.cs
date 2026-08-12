using i26.Core.DomainEvents;
using i26.EntityFrameworkCore.DomainEvents;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace i26.EntityFrameworkCore.Tests.DomainEvents;

/// <summary>
/// Against a real database, because what is being tested is timing: when the events leave the
/// entities, and when they are allowed to go out — which depends on a save having succeeded and on
/// whether someone else is holding a transaction open.
/// </summary>
public sealed class DomainEventInterceptorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly RecordingDispatcher _dispatcher = new();
    private readonly DomainEventQueue _queue;

    public DomainEventInterceptorTests()
    {
        _queue = new DomainEventQueue(_dispatcher);
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private TicketDbContext CreateContext(
        DomainEventPublishing publishing = DomainEventPublishing.AfterSaveChanges) =>
        new(new DbContextOptionsBuilder<TicketDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new DomainEventInterceptor(_queue, publishing))
            .Options);

    [Fact]
    public async Task A_saved_entity_has_its_events_published()
    {
        using var context = CreateContext();
        context.Tickets.Add(Ticket.Open("disk is full"));

        await context.SaveChangesAsync();

        var opened = Assert.IsType<TicketOpened>(Assert.Single(_dispatcher.Dispatched));
        Assert.Equal("disk is full", opened.Title);
    }

    [Fact]
    public async Task The_entity_no_longer_holds_what_was_published()
    {
        using var context = CreateContext();
        var ticket = Ticket.Open("disk is full");
        context.Tickets.Add(ticket);

        await context.SaveChangesAsync();

        Assert.Empty(ticket.DomainEvents);
    }

    [Fact]
    public async Task A_second_save_does_not_publish_the_same_event_again()
    {
        using var context = CreateContext();
        var ticket = Ticket.Open("disk is full");
        context.Tickets.Add(ticket);

        await context.SaveChangesAsync();
        ticket.Rename("disk is nearly full");
        await context.SaveChangesAsync();

        Assert.Equal(
            [typeof(TicketOpened), typeof(TicketRenamed)],
            _dispatcher.Dispatched.Select(domainEvent => domainEvent.GetType()));
    }

    [Fact]
    public async Task An_entity_being_deleted_still_has_its_events_collected()
    {
        using var seeding = CreateContext();
        var ticket = Ticket.Open("disk is full");
        seeding.Tickets.Add(ticket);
        await seeding.SaveChangesAsync();
        _dispatcher.Dispatched.Clear();

        // The removed entity is detached from the change tracker by the time the save completes,
        // so an interceptor that only looked afterwards would never see this event.
        ticket.Close();
        seeding.Tickets.Remove(ticket);
        await seeding.SaveChangesAsync();

        Assert.IsType<TicketClosed>(Assert.Single(_dispatcher.Dispatched));
    }

    [Fact]
    public async Task A_save_that_fails_publishes_nothing()
    {
        using var context = CreateContext();

        // A title the column refuses: the insert fails, and so does the save around it.
        context.Tickets.Add(Ticket.Open(null!));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Empty(_dispatcher.Dispatched);
    }

    [Fact]
    public async Task Events_wait_while_a_transaction_is_open()
    {
        using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        context.Tickets.Add(Ticket.Open("disk is full"));
        await context.SaveChangesAsync();

        Assert.Empty(_dispatcher.Dispatched);
        Assert.Single(_queue.Pending);
    }

    [Fact]
    public async Task Whoever_committed_the_transaction_publishes_them()
    {
        using var context = CreateContext();

        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            context.Tickets.Add(Ticket.Open("disk is full"));
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await _queue.PublishAsync();

        Assert.IsType<TicketOpened>(Assert.Single(_dispatcher.Dispatched));
    }

    [Fact]
    public async Task A_rolled_back_transaction_takes_the_events_with_it()
    {
        using var context = CreateContext();

        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            context.Tickets.Add(Ticket.Open("disk is full"));
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();

            // What a transaction decorator does on the failing branch: nothing was committed, so
            // nothing is published and the queue is emptied with the unit of work.
            _queue.Clear();
        }

        await _queue.PublishAsync();

        Assert.Empty(_dispatcher.Dispatched);
    }

    [Fact]
    public async Task Publication_left_to_the_caller_is_left_to_the_caller()
    {
        using var context = CreateContext(DomainEventPublishing.Manual);
        context.Tickets.Add(Ticket.Open("disk is full"));

        await context.SaveChangesAsync();

        Assert.Empty(_dispatcher.Dispatched);
        Assert.Single(_queue.Pending);

        await _queue.PublishAsync();

        Assert.IsType<TicketOpened>(Assert.Single(_dispatcher.Dispatched));
    }

    [Fact]
    public async Task A_synchronous_save_collects_and_leaves_the_publishing_to_a_call_that_can_await()
    {
        using var context = CreateContext();
        context.Tickets.Add(Ticket.Open("disk is full"));

        context.SaveChanges();

        Assert.Empty(_dispatcher.Dispatched);
        Assert.Single(_queue.Pending);

        await _queue.PublishAsync();

        Assert.IsType<TicketOpened>(Assert.Single(_dispatcher.Dispatched));
    }

    [Fact]
    public async Task An_entity_that_raised_nothing_publishes_nothing()
    {
        using var context = CreateContext();
        var ticket = Ticket.Open("disk is full");
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
        _dispatcher.Dispatched.Clear();

        await context.SaveChangesAsync();

        Assert.Empty(_dispatcher.Dispatched);
    }

    [Fact]
    public void The_events_stay_out_of_the_model_with_nothing_configured()
    {
        using var context = CreateContext();

        var ticket = context.Model.FindEntityType(typeof(Ticket))!;

        // Nobody ignores DomainEvents here, and nobody has to: a get-only list of an interface is
        // neither a primitive collection nor a navigation candidate. The day that changes, this is
        // the test that says so, and an Ignore convention is what it would take.
        Assert.Null(ticket.FindProperty(nameof(IHasDomainEvents.DomainEvents)));
        Assert.Null(ticket.FindNavigation(nameof(IHasDomainEvents.DomainEvents)));
        Assert.Null(context.Model.FindEntityType(typeof(TicketOpened)));
        Assert.Equal(["Id", "Title"], ticket.GetProperties().Select(property => property.Name));
    }
}
