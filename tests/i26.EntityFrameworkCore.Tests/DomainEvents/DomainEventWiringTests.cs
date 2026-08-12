using i26.Core.DomainEvents;
using i26.EntityFrameworkCore.DomainEvents;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace i26.EntityFrameworkCore.Tests.DomainEvents;

/// <summary>
/// The wiring the README documents, resolved out of a real container: the queue is scoped, so the
/// interceptor of a context and the code that publishes after a commit are looking at the same one.
/// </summary>
public sealed class DomainEventWiringTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DomainEventWiringTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private ServiceProvider BuildProvider(
        DomainEventPublishing publishing = DomainEventPublishing.AfterSaveChanges)
    {
        var services = new ServiceCollection();

        services.AddScoped<RecordingDispatcher>();
        services.AddScoped<IDomainEventDispatcher>(provider => provider.GetRequiredService<RecordingDispatcher>());
        services.AddScoped<DomainEventQueue>();

        services.AddDbContext<TicketDbContext>((provider, options) => options
            .UseSqlite(_connection)
            .UseDomainEvents(provider, publishing));

        var built = services.BuildServiceProvider();

        using (var scope = built.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<TicketDbContext>().Database.EnsureCreated();
        }

        return built;
    }

    [Fact]
    public async Task A_context_resolved_from_the_scope_publishes_into_the_queue_of_that_scope()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
        context.Tickets.Add(Ticket.Open("disk is full"));

        await context.SaveChangesAsync();

        Assert.IsType<TicketOpened>(
            Assert.Single(scope.ServiceProvider.GetRequiredService<RecordingDispatcher>().Dispatched));
    }

    [Fact]
    public async Task What_one_scope_saves_does_not_reach_another_scope()
    {
        using var provider = BuildProvider();

        using var saving = provider.CreateScope();
        using var idle = provider.CreateScope();

        var context = saving.ServiceProvider.GetRequiredService<TicketDbContext>();
        context.Tickets.Add(Ticket.Open("disk is full"));
        await context.SaveChangesAsync();

        Assert.Empty(idle.ServiceProvider.GetRequiredService<RecordingDispatcher>().Dispatched);
    }

    [Fact]
    public async Task The_transaction_owner_publishes_the_queue_it_shares_with_the_context()
    {
        using var provider = BuildProvider(DomainEventPublishing.Manual);
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<DomainEventQueue>();

        // What a transaction decorator does around a command handler.
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            context.Tickets.Add(Ticket.Open("disk is full"));
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await queue.PublishAsync();

        Assert.IsType<TicketOpened>(
            Assert.Single(scope.ServiceProvider.GetRequiredService<RecordingDispatcher>().Dispatched));
    }

    [Fact]
    public void Without_a_queue_in_the_scope_the_wiring_says_so()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TicketDbContext>((provider, options) => options
            .UseSqlite(_connection)
            .UseDomainEvents(provider));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var exception = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<TicketDbContext>());

        Assert.Contains(nameof(DomainEventQueue), exception.Message, StringComparison.Ordinal);
        Assert.Contains("AddDomainEvents", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void It_refuses_a_null_argument()
    {
        var builder = new DbContextOptionsBuilder<TicketDbContext>();

        Assert.Throws<ArgumentNullException>(() =>
            ((DbContextOptionsBuilder)null!).UseDomainEvents(new ServiceCollection().BuildServiceProvider()));

        Assert.Throws<ArgumentNullException>(() => builder.UseDomainEvents(null!));
        Assert.Throws<ArgumentNullException>(() => new DomainEventInterceptor(null!));
    }
}
