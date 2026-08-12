using i26.Core.Ids;
using i26.Core.Pagination;
using i26.EntityFrameworkCore.Ids;
using i26.EntityFrameworkCore.Pagination;
using i26.EntityFrameworkCore.Tests.Ids;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace i26.EntityFrameworkCore.Tests.Pagination;

/// <summary>
/// Paging a table whose tie-breaker is a typed id rather than a raw <see cref="Guid"/>. The keyset
/// predicate has to reach SQL as a comparison on the column: Entity Framework refuses to evaluate a
/// <c>Where</c> it cannot translate, so every one of these failing to translate is a failing test
/// rather than a page quietly read in memory.
/// </summary>
public sealed class TypedIdPagingTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 8, 11, 20, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;

    public TypedIdPagingTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private ArticleDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ArticleDbContext>().UseSqlite(_connection).Options);

    private void Seed(int count, bool sharingAnInstant = false)
    {
        using var context = CreateContext();

        for (var index = 0; index < count; index++)
        {
            context.Articles.Add(new Article
            {
                Id = ArticleId.New(),
                Title = $"article {index:D3}",
                CreatedAt = sharingAnInstant ? Start : Start.AddSeconds(index),
            });
        }

        context.SaveChanges();
    }

    private async Task<List<Article>> ReadEveryPageAsync(int limit)
    {
        var rows = new List<Article>();
        string? cursor = null;

        for (var page = 0; page < 100; page++)
        {
            using var context = CreateContext();

            var result = await context.Articles.ToPagedResponseAsync<Article, ArticleId>(
                new CursorPageRequest { Limit = limit, Cursor = cursor });

            rows.AddRange(result.Value.Items);

            if (!result.Value.HasNext)
            {
                return rows;
            }

            cursor = result.Value.Cursor;
        }

        throw new InvalidOperationException("The pages never ran out.");
    }

    [Fact]
    public async Task A_page_comes_back_newest_first()
    {
        Seed(5);

        using var context = CreateContext();

        var page = await context.Articles.ToPagedResponseAsync<Article, ArticleId>(
            new CursorPageRequest { Limit = 3 });

        Assert.Equal(
            ["article 004", "article 003", "article 002"],
            page.Value.Items.Select(article => article.Title));

        Assert.True(page.Value.HasNext);
    }

    [Fact]
    public async Task Walking_the_pages_reads_every_row_once()
    {
        Seed(47);

        var rows = await ReadEveryPageAsync(limit: 10);

        Assert.Equal(47, rows.Count);
        Assert.Equal(47, rows.Select(article => article.Id).Distinct().Count());
    }

    [Fact]
    public async Task Rows_sharing_an_instant_are_still_cut_cleanly()
    {
        // Everything at the same instant, so only the id keeps the boundary from drifting — which
        // means the comparison on the typed id column is doing the work.
        Seed(25, sharingAnInstant: true);

        var rows = await ReadEveryPageAsync(limit: 7);

        Assert.Equal(25, rows.Count);
        Assert.Equal(25, rows.Select(article => article.Id).Distinct().Count());
    }

    [Fact]
    public async Task The_tie_break_orders_by_the_id_the_way_the_id_does()
    {
        Seed(12, sharingAnInstant: true);

        var rows = await ReadEveryPageAsync(limit: 5);

        // Descending, and the database agreeing with TypedId.Compare is the whole point of storing
        // the ids in an order-preserving encoding.
        Assert.Equal(rows.OrderByDescending(article => article.Id).Select(article => article.Id), rows.Select(article => article.Id));
    }

    [Fact]
    public async Task The_cursor_carries_the_id_in_its_own_textual_form()
    {
        Seed(5);

        using var context = CreateContext();

        var page = await context.Articles.ToPagedResponseAsync<Article, ArticleId>(
            new CursorPageRequest { Limit = 3 });

        Assert.NotNull(page.Value.Cursor);
        Assert.True(Cursor.TryDecode<ArticleId>(page.Value.Cursor, out _, out var id));
        Assert.Equal(page.Value.Items[^1].Id, id);
    }

    [Fact]
    public async Task A_cursor_holding_another_entity_s_id_is_a_validation_failure()
    {
        Seed(5);

        using var context = CreateContext();

        var page = await context.Articles.ToPagedResponseAsync<Article, ArticleId>(
            new CursorPageRequest { Cursor = Cursor.Encode(Start, OrderId.New()) });

        Assert.True(page.IsFailure);
        Assert.Equal(PaginationErrors.InvalidCursor, page.Error);
    }

    [Fact]
    public async Task A_projection_is_paged_in_the_database_not_in_memory()
    {
        Seed(12);

        using var context = CreateContext();

        var page = await context.Articles
            .Select(article => new ArticleRow
            {
                Id = article.Id,
                Title = article.Title,
                CreatedAt = article.CreatedAt,
            })
            .ToPagedResponseAsync<ArticleRow, ArticleId>(new CursorPageRequest { Limit = 5 });

        Assert.Equal(5, page.Value.Items.Count);
        Assert.Equal("article 011", page.Value.Items[0].Title);
        Assert.True(page.Value.HasNext);
    }

    private sealed record ArticleRow : ICursorPageable<ArticleId>
    {
        public required ArticleId Id { get; init; }

        public required string Title { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }
    }

    private sealed class Article : ICursorPageable<ArticleId>
    {
        public ArticleId Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }
    }

    private sealed class ArticleDbContext(DbContextOptions<ArticleDbContext> options) : DbContext(options)
    {
        public DbSet<Article> Articles => Set<Article>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // ProviderDefault, not the Postgres shape: "text COLLATE C" is Postgres' vocabulary, and
            // SQLite's own default for a text column is already the byte-by-byte order the ids want.
            configurationBuilder.ApplyTypedIdConventions(
                TypedIdStorage.ProviderDefault,
                typeof(ArticleId).Assembly);

            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }
}

/// <summary>An id written by the generator, so the operators under test are the generated ones.</summary>
[TypedId("art")]
public readonly partial record struct ArticleId;
