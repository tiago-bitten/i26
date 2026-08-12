using System.Linq.Expressions;
using i26.Core.Queries;
using i26.Core.Specifications;
using i26.EntityFrameworkCore.Queries;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace i26.EntityFrameworkCore.Tests.Queries;

/// <summary>
/// A specification is only worth writing once if the database reads the same rule the entity does.
/// These assert the SQL, not the count: a rule that quietly filtered in memory would return exactly
/// the same rows and read the whole table to do it.
/// </summary>
public sealed class SpecificationQueryTests : IDisposable
{
    private sealed class Large(decimal atLeast) : Specification<Invoice>
    {
        public override Expression<Func<Invoice, bool>> ToExpression() => invoice => invoice.Amount >= atLeast;
    }

    private sealed class Numbered(int number) : Specification<Invoice>
    {
        public override Expression<Func<Invoice, bool>> ToExpression() => invoice => invoice.Number == number;
    }

    private readonly SqliteConnection _connection;
    private readonly List<string> _sql = [];

    public SpecificationQueryTests()
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
                CreatedAt = DateTimeOffset.UnixEpoch.AddSeconds(number),
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

    [Fact]
    public void A_specification_filters_in_the_database()
    {
        using var context = CreateContext();

        var matches = context.Invoices.Where(new Large(20m)).ToList();

        // Ordered here rather than in the query: nothing asked the database for an order, so the
        // rows come back in whichever one the plan produced.
        Assert.Equal([2, 3], matches.Select(invoice => invoice.Number).Order());
        Assert.Contains(_sql, entry => entry.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_composed_specification_is_one_statement()
    {
        using var context = CreateContext();
        var specification = new Large(20m).And(new Numbered(3));

        var matches = context.Invoices.Where(specification).ToList();

        Assert.Equal([3], matches.Select(invoice => invoice.Number));

        // One round trip, and everything the rule says is in it.
        var statement = Assert.Single(_sql, entry => entry.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("WHERE", statement, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Or_and_Not_reach_the_database_too()
    {
        using var context = CreateContext();

        var either = context.Invoices.Where(new Numbered(1).Or(new Numbered(3))).ToList();
        var neither = context.Invoices.Where(new Numbered(1).Or(new Numbered(3)).Not()).ToList();

        Assert.Equal([1, 3], either.Select(invoice => invoice.Number).Order());
        Assert.Equal([2], neither.Select(invoice => invoice.Number));
        Assert.All(_sql, entry => Assert.Contains("WHERE", entry, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_filter_that_does_not_apply_leaves_no_trace_in_the_statement()
    {
        using var context = CreateContext();

        var all = context.Invoices.WhereIf(condition: false, new Large(20m)).ToList();

        Assert.Equal(3, all.Count);
        Assert.DoesNotContain(_sql, entry => entry.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_specification_goes_through_the_executor_like_any_other_query()
    {
        using var context = CreateContext();
        var executor = new AsyncQueryExecutor([EfCoreAsyncQueryBackend.Default]);

        var matches = await executor.CountAsync(context.Invoices.Where(new Large(20m)));

        Assert.Equal(2, matches);
        Assert.Contains(_sql, entry => entry.Contains("COUNT(*)", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_rule_the_database_answers_is_the_rule_the_entity_answers()
    {
        using var context = CreateContext();
        var specification = new Large(20m).And(new Numbered(3));

        var fromDatabase = context.Invoices.Where(specification).Select(invoice => invoice.Number).ToList();
        var fromMemory = context.Invoices
            .AsEnumerable()
            .Where(specification.IsSatisfiedBy)
            .Select(invoice => invoice.Number)
            .ToList();

        Assert.Equal(fromMemory, fromDatabase);
    }
}
