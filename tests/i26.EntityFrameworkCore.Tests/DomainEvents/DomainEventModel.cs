using i26.Core.DomainEvents;
using Microsoft.EntityFrameworkCore;

namespace i26.EntityFrameworkCore.Tests.DomainEvents;

public sealed record TicketOpened(Guid Id, string Title) : IDomainEvent;

public sealed record TicketRenamed(Guid Id, string Title) : IDomainEvent;

public sealed record TicketClosed(Guid Id) : IDomainEvent;

/// <summary>
/// An entity that raises events without inheriting anything: a list, the two members of
/// <see cref="IHasDomainEvents"/>, and behaviour that raises.
/// </summary>
public sealed class Ticket : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; init; }

    public string Title { get; private set; } = string.Empty;

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    public static Ticket Open(string title)
    {
        var ticket = new Ticket { Id = Guid.NewGuid(), Title = title };
        ticket._domainEvents.Add(new TicketOpened(ticket.Id, title));

        return ticket;
    }

    public void Rename(string title)
    {
        Title = title;
        _domainEvents.Add(new TicketRenamed(Id, title));
    }

    /// <summary>Raised by the entity, on the way to being removed from the set.</summary>
    public void Close() => _domainEvents.Add(new TicketClosed(Id));
}

public sealed class TicketDbContext(DbContextOptions<TicketDbContext> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    // Nothing here ignores the events: a read-only list of an interface is not a navigation
    // candidate, and Entity Framework leaves it alone on its own. See the test that pins it.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Ticket>().Property(ticket => ticket.Title).IsRequired();
}

/// <summary>Records what it was handed, standing in for handlers that ran.</summary>
public sealed class RecordingDispatcher : IDomainEventDispatcher
{
    public List<IDomainEvent> Dispatched { get; } = [];

    public Task DispatchAsync(
        IReadOnlyList<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        Dispatched.AddRange(domainEvents);

        return Task.CompletedTask;
    }
}
