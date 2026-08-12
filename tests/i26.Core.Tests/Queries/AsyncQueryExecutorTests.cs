using System.Linq.Expressions;
using i26.Core.Queries;

namespace i26.Core.Tests.Queries;

/// <summary>
/// The executor over a plain list, which is the case an application service is unit tested in: no
/// backend can run it asynchronously, and every answer still has to be right.
/// </summary>
public sealed class AsyncQueryExecutorTests
{
    private sealed record Invoice(int Number, decimal Amount);

    private static readonly Invoice[] Invoices =
    [
        new(1, 10m),
        new(2, 20m),
        new(3, 30m),
    ];

    private static IQueryable<Invoice> Query => Invoices.AsQueryable();

    private static AsyncQueryExecutor WithoutBackends() => new([]);

    /// <summary>Answers every query, and records the ones it was asked to run.</summary>
    private sealed class RecordingBackend(bool canExecute) : IAsyncQueryBackend
    {
        public List<string> Ran { get; } = [];

        public bool CanExecute(IQueryable query) => canExecute;

        public Task<TResult> ExecuteAsync<T, TResult>(
            IQueryable<T> query,
            Expression<Func<IQueryable<T>, TResult>> terminal,
            CancellationToken cancellationToken = default)
        {
            Ran.Add(terminal.ToString());

            return Task.FromResult(terminal.Compile()(query));
        }

        public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        {
            Ran.Add("ToList");

            return Task.FromResult(query.ToList());
        }
    }

    [Fact]
    public async Task An_operator_still_runs_when_no_backend_can_take_it()
    {
        var executor = WithoutBackends();

        Assert.Equal(3, await executor.CountAsync(Query));
        Assert.Equal(Invoices, await executor.ToListAsync(Query));
    }

    [Fact]
    public async Task The_backend_that_says_it_can_gets_the_query()
    {
        var backend = new RecordingBackend(canExecute: true);
        var executor = new AsyncQueryExecutor([backend]);

        await executor.CountAsync(Query);
        await executor.ToListAsync(Query);

        Assert.Equal(2, backend.Ran.Count);
    }

    [Fact]
    public async Task A_backend_that_says_it_cannot_is_not_asked()
    {
        var backend = new RecordingBackend(canExecute: false);
        var executor = new AsyncQueryExecutor([backend]);

        Assert.Equal(3, await executor.CountAsync(Query));
        Assert.Empty(backend.Ran);
    }

    [Fact]
    public async Task The_first_backend_that_can_wins()
    {
        var first = new RecordingBackend(canExecute: false);
        var second = new RecordingBackend(canExecute: true);
        var third = new RecordingBackend(canExecute: true);

        await new AsyncQueryExecutor([first, second, third]).CountAsync(Query);

        Assert.Empty(first.Ran);
        Assert.Single(second.Ran);
        Assert.Empty(third.Ran);
    }

    [Fact]
    public async Task An_operator_nobody_wrote_a_method_for_is_the_operator_itself()
    {
        var executor = WithoutBackends();

        Assert.Equal(60m, await executor.ExecuteAsync(Query, invoices => invoices.Sum(invoice => invoice.Amount)));
        Assert.Equal(30m, await executor.ExecuteAsync(Query, invoices => invoices.Max(invoice => invoice.Amount)));
        Assert.Equal(20m, await executor.ExecuteAsync(Query, invoices => invoices.Average(invoice => invoice.Amount)));
    }

    [Fact]
    public async Task Counting()
    {
        var executor = WithoutBackends();

        Assert.Equal(3, await executor.CountAsync(Query));
        Assert.Equal(2, await executor.CountAsync(Query, invoice => invoice.Amount > 10m));
        Assert.Equal(3L, await executor.LongCountAsync(Query));
        Assert.Equal(2L, await executor.LongCountAsync(Query, invoice => invoice.Amount > 10m));
    }

    [Fact]
    public async Task Asking_whether_anything_matches()
    {
        var executor = WithoutBackends();

        Assert.True(await executor.AnyAsync(Query));
        Assert.False(await executor.AnyAsync(Query.Where(invoice => invoice.Amount > 100m)));
        Assert.True(await executor.AnyAsync(Query, invoice => invoice.Number == 2));
        Assert.False(await executor.AnyAsync(Query, invoice => invoice.Number == 9));
    }

    [Fact]
    public async Task Asking_whether_everything_matches()
    {
        var executor = WithoutBackends();

        Assert.True(await executor.AllAsync(Query, invoice => invoice.Amount > 0m));
        Assert.False(await executor.AllAsync(Query, invoice => invoice.Amount > 10m));
    }

    [Fact]
    public async Task Taking_one_row()
    {
        var executor = WithoutBackends();

        Assert.Equal(Invoices[0], await executor.FirstAsync(Query));
        Assert.Equal(Invoices[1], await executor.FirstAsync(Query, invoice => invoice.Number == 2));
        Assert.Equal(Invoices[2], await executor.SingleAsync(Query, invoice => invoice.Number == 3));
        Assert.Equal(Invoices[0], await executor.SingleAsync(Query.Where(invoice => invoice.Number == 1)));
    }

    [Fact]
    public async Task Taking_one_row_that_may_not_be_there()
    {
        var executor = WithoutBackends();
        var empty = Query.Where(invoice => invoice.Number == 9);

        Assert.Null(await executor.FirstOrDefaultAsync(empty));
        Assert.Null(await executor.FirstOrDefaultAsync(Query, invoice => invoice.Number == 9));
        Assert.Null(await executor.SingleOrDefaultAsync(empty));
        Assert.Null(await executor.SingleOrDefaultAsync(Query, invoice => invoice.Number == 9));
        Assert.Equal(Invoices[1], await executor.FirstOrDefaultAsync(Query, invoice => invoice.Number == 2));
    }

    [Fact]
    public async Task Reading_every_row()
    {
        var executor = WithoutBackends();

        Assert.Equal(Invoices, await executor.ToListAsync(Query));
        Assert.Equal(Invoices, await executor.ToArrayAsync(Query));
    }

    [Fact]
    public async Task It_refuses_a_null_argument()
    {
        var executor = WithoutBackends();

        Assert.Throws<ArgumentNullException>(() => new AsyncQueryExecutor(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => executor.ToListAsync<Invoice>(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => executor.ExecuteAsync<Invoice, int>(null!, invoices => invoices.Count()));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => executor.ExecuteAsync<Invoice, int>(Query, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => executor.CountAsync<Invoice>(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => executor.AllAsync<Invoice>(Query, null!));
    }
}
