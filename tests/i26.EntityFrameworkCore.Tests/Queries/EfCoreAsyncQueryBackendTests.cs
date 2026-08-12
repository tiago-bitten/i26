using i26.Core.Pagination;
using i26.Core.Queries;
using i26.EntityFrameworkCore.Pagination;
using i26.EntityFrameworkCore.Queries;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace i26.EntityFrameworkCore.Tests.Queries;

/// <summary>
/// Against a real database, because the claim being tested is that the operator reaches SQL — an
/// executor that quietly ran everything on the client would pass every assertion about the answers
/// and none about the statements.
/// </summary>
public sealed class EfCoreAsyncQueryBackendTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 8, 12, 20, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly List<string> _sql = [];

    public EfCoreAsyncQueryBackendTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();

        for (var number = 1; number <= 3; number++)
        {
            context.Invoices.Add(new Invoice
            {
                Id = Guid.NewGuid(),
                Number = number,
                Amount = number * 10m,
                CreatedAt = Start.AddSeconds(number),
            });
        }

        context.SaveChanges();
        _sql.Clear();
    }

    public void Dispose() => _connection.Dispose();

    private InvoiceDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<InvoiceDbContext>()
            .UseSqlite(_connection)
            .LogTo(_sql.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
            .Options);

    private static IAsyncQueryExecutor Executor => new AsyncQueryExecutor([EfCoreAsyncQueryBackend.Default]);

    [Fact]
    public void An_entity_framework_query_is_one_this_backend_can_run()
    {
        using var context = CreateContext();

        Assert.True(EfCoreAsyncQueryBackend.Default.CanExecute(context.Invoices));
        Assert.False(EfCoreAsyncQueryBackend.Default.CanExecute(new[] { 1, 2 }.AsQueryable()));
    }

    [Fact]
    public async Task Counting_counts_in_the_database()
    {
        using var context = CreateContext();

        Assert.Equal(3, await Executor.CountAsync(context.Invoices));
        Assert.Contains(_sql, entry => entry.Contains("COUNT(*)", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task An_operator_with_no_method_of_its_own_reaches_the_database_too()
    {
        using var context = CreateContext();

        // Summed over the int and not the decimal: SQLite refuses that aggregate on decimal, which
        // is a limit of the test database rather than of the seam under test.
        var total = await Executor.ExecuteAsync(context.Invoices, invoices => invoices.Sum(invoice => invoice.Number));

        Assert.Equal(6, total);
        Assert.Contains(_sql, entry => entry.Contains("SUM(", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_operators_answer_what_they_answer_in_memory()
    {
        using var context = CreateContext();
        var invoices = context.Invoices.OrderBy(invoice => invoice.Number);

        Assert.Equal(2, await Executor.CountAsync(invoices, invoice => invoice.Amount > 10m));
        Assert.Equal(3L, await Executor.LongCountAsync(invoices));
        Assert.True(await Executor.AnyAsync(invoices, invoice => invoice.Number == 2));
        Assert.False(await Executor.AnyAsync(invoices, invoice => invoice.Number == 9));
        Assert.True(await Executor.AllAsync(invoices, invoice => invoice.Amount > 0m));
        Assert.False(await Executor.AllAsync(invoices, invoice => invoice.Amount > 10m));
        Assert.Equal(1, (await Executor.FirstAsync(invoices)).Number);
        Assert.Equal(2, (await Executor.SingleAsync(invoices, invoice => invoice.Number == 2)).Number);
        Assert.Null(await Executor.FirstOrDefaultAsync(invoices, invoice => invoice.Number == 9));
        Assert.Null(await Executor.SingleOrDefaultAsync(invoices, invoice => invoice.Number == 9));
        Assert.Equal(3, (await Executor.ToListAsync(invoices)).Count);
        Assert.Equal(3, (await Executor.ToArrayAsync(invoices)).Length);
    }

    [Fact]
    public async Task A_predicate_travels_as_a_predicate_and_not_as_an_object()
    {
        using var context = CreateContext();

        // The trap this shape avoids: an Expression handed to Queryable.Count as a captured
        // variable is a member access, which no provider can read as a lambda.
        var matches = await Executor.CountAsync(context.Invoices, invoice => invoice.Amount > 10m);

        Assert.Equal(2, matches);
        Assert.Contains(_sql, entry => entry.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task With_no_backend_registered_the_answer_is_still_right()
    {
        using var context = CreateContext();

        var executor = new AsyncQueryExecutor([]);

        // Synchronous, on this thread, and documented as such — but not wrong.
        Assert.Equal(3, await executor.CountAsync(context.Invoices));
        Assert.Equal(3, (await executor.ToListAsync(context.Invoices)).Count);
    }

    [Fact]
    public async Task Reaching_for_the_backend_with_a_query_it_cannot_run_says_so()
    {
        var rows = new[] { 1, 2, 3 }.AsQueryable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => EfCoreAsyncQueryBackend.Default.ToListAsync(rows));

        Assert.Contains(nameof(IAsyncQueryExecutor), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Paging_through_the_executor_pages_the_way_the_entity_framework_overload_does()
    {
        using var context = CreateContext();
        var request = new CursorPageRequest { Limit = 2, IncludeTotal = true };

        var throughExecutor = await context.Invoices.ToPagedResponseAsync(Executor, request);
        var throughEntityFramework = await context.Invoices.ToPagedResponseAsync(request);

        Assert.Equal(
            throughEntityFramework.Value.Items.Select(invoice => invoice.Number),
            throughExecutor.Value.Items.Select(invoice => invoice.Number));

        Assert.Equal(throughEntityFramework.Value.Cursor, throughExecutor.Value.Cursor);
        Assert.Equal(3, throughExecutor.Value.Total);
        Assert.True(throughExecutor.Value.HasNext);
    }

    [Fact]
    public void The_registration_wires_the_backend_behind_the_executor()
    {
        var services = new ServiceCollection();

        services.AddEfCoreAsyncQueries();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<AsyncQueryExecutor>(provider.GetRequiredService<IAsyncQueryExecutor>());
        Assert.IsType<EfCoreAsyncQueryBackend>(Assert.Single(provider.GetServices<IAsyncQueryBackend>()));
    }

    [Fact]
    public void Registering_twice_leaves_one_backend()
    {
        var services = new ServiceCollection();

        services.AddEfCoreAsyncQueries();
        services.AddEfCoreAsyncQueries();

        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetServices<IAsyncQueryBackend>());
    }

    [Fact]
    public void An_executor_of_your_own_is_kept()
    {
        var services = new ServiceCollection();
        var mine = new AsyncQueryExecutor([]);

        services.AddSingleton<IAsyncQueryExecutor>(mine);
        services.AddEfCoreAsyncQueries();

        using var provider = services.BuildServiceProvider();

        Assert.Same(mine, Assert.Single(provider.GetServices<IAsyncQueryExecutor>()));
    }
}
