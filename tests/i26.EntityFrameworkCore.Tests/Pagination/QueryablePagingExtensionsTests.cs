using i26.Core.Pagination;
using i26.EntityFrameworkCore.Pagination;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace i26.EntityFrameworkCore.Tests.Pagination;

/// <summary>
/// Paging against a real database. The interesting parts — that a page never repeats or skips a
/// row, and that rows sharing an instant are still cut cleanly — only show up when a query planner
/// is involved.
/// </summary>
public sealed class QueryablePagingExtensionsTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 8, 11, 20, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;

    public QueryablePagingExtensionsTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private PagingDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PagingDbContext>().UseSqlite(_connection).Options);

    /// <summary>Seeds <paramref name="count"/> notes, one second apart, newest last.</summary>
    private void Seed(int count)
    {
        using var context = CreateContext();

        for (var index = 0; index < count; index++)
        {
            context.Notes.Add(new Note
            {
                Id = Guid.NewGuid(),
                Title = $"note {index:D3}",
                CreatedAt = Start.AddSeconds(index),
            });
        }

        context.SaveChanges();
    }

    /// <summary>Walks every page and returns the rows in the order they came out.</summary>
    private async Task<List<Note>> ReadEveryPageAsync(int limit)
    {
        var rows = new List<Note>();
        string? cursor = null;

        for (var page = 0; page < 100; page++)
        {
            using var context = CreateContext();

            var result = await context.Notes.ToPagedResponseAsync(
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

        var page = await context.Notes.ToPagedResponseAsync(new CursorPageRequest { Limit = 3 });

        Assert.Equal(["note 004", "note 003", "note 002"], page.Value.Items.Select(note => note.Title));
        Assert.True(page.Value.HasNext);
        Assert.NotNull(page.Value.Cursor);
    }

    [Fact]
    public async Task Walking_the_pages_reads_every_row_once()
    {
        Seed(47);

        var rows = await ReadEveryPageAsync(limit: 10);

        Assert.Equal(47, rows.Count);
        Assert.Equal(47, rows.Select(note => note.Id).Distinct().Count());
        Assert.Equal(rows.OrderByDescending(note => note.CreatedAt).Select(note => note.Title), rows.Select(note => note.Title));
    }

    [Fact]
    public async Task It_reads_every_row_once_even_when_the_page_divides_the_rows_exactly()
    {
        Seed(20);

        var rows = await ReadEveryPageAsync(limit: 10);

        Assert.Equal(20, rows.Count);
        Assert.Equal(20, rows.Select(note => note.Id).Distinct().Count());
    }

    [Fact]
    public async Task Rows_sharing_an_instant_are_still_cut_cleanly()
    {
        // Everything at the same instant: only the id keeps the boundary from drifting.
        using (var context = CreateContext())
        {
            for (var index = 0; index < 25; index++)
            {
                context.Notes.Add(new Note { Id = Guid.NewGuid(), Title = $"tie {index:D3}", CreatedAt = Start });
            }

            context.SaveChanges();
        }

        var rows = await ReadEveryPageAsync(limit: 7);

        Assert.Equal(25, rows.Count);
        Assert.Equal(25, rows.Select(note => note.Id).Distinct().Count());
    }

    [Fact]
    public async Task The_last_page_hands_out_no_cursor()
    {
        Seed(3);

        using var context = CreateContext();

        var page = await context.Notes.ToPagedResponseAsync(new CursorPageRequest { Limit = 10 });

        Assert.Equal(3, page.Value.Items.Count);
        Assert.False(page.Value.HasNext);
        Assert.Null(page.Value.Cursor);
    }

    [Fact]
    public async Task An_empty_table_is_an_empty_page()
    {
        using var context = CreateContext();

        var page = await context.Notes.ToPagedResponseAsync(new CursorPageRequest());

        Assert.Empty(page.Value.Items);
        Assert.False(page.Value.HasNext);
        Assert.Null(page.Value.Cursor);
    }

    [Fact]
    public async Task The_filter_of_the_query_is_kept_across_pages()
    {
        Seed(30);

        using var context = CreateContext();

        var page = await context.Notes
            .Where(note => note.Title.EndsWith("0"))
            .ToPagedResponseAsync(new CursorPageRequest { Limit = 2, IncludeTotal = true });

        Assert.Equal(3, page.Value.Total);
        Assert.All(page.Value.Items, note => Assert.EndsWith("0", note.Title, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_total_counts_the_whole_match_not_the_page()
    {
        Seed(30);

        using var context = CreateContext();

        var page = await context.Notes.ToPagedResponseAsync(
            new CursorPageRequest { Limit = 5, IncludeTotal = true });

        Assert.Equal(30, page.Value.Total);
        Assert.Equal(5, page.Value.Items.Count);
    }

    [Fact]
    public async Task The_total_is_left_out_unless_it_was_asked_for()
    {
        Seed(30);

        using var context = CreateContext();

        var page = await context.Notes.ToPagedResponseAsync(new CursorPageRequest { Limit = 5 });

        Assert.Null(page.Value.Total);
    }

    [Fact]
    public async Task The_total_stays_the_same_on_the_second_page()
    {
        Seed(30);

        using var context = CreateContext();

        var first = await context.Notes.ToPagedResponseAsync(
            new CursorPageRequest { Limit = 5, IncludeTotal = true });

        var second = await context.Notes.ToPagedResponseAsync(
            new CursorPageRequest { Limit = 5, IncludeTotal = true, Cursor = first.Value.Cursor });

        Assert.Equal(30, second.Value.Total);
    }

    [Fact]
    public async Task A_cursor_that_did_not_come_from_here_is_a_validation_failure()
    {
        Seed(3);

        using var context = CreateContext();

        var page = await context.Notes.ToPagedResponseAsync(
            new CursorPageRequest { Cursor = "made up" });

        Assert.True(page.IsFailure);
        Assert.Equal(PaginationErrors.InvalidCursor, page.Error);
        Assert.Equal(400, page.StatusCode);
    }

    [Fact]
    public async Task The_limit_is_clamped_to_the_ceiling()
    {
        Seed(30);

        using var context = CreateContext();

        var page = await context.Notes.ToPagedResponseAsync(
            new CursorPageRequest { Limit = 1_000 },
            maxLimit: 8);

        Assert.Equal(8, page.Value.Items.Count);
        Assert.True(page.Value.HasNext);
    }

    [Fact]
    public async Task A_projection_is_paged_in_the_database_not_in_memory()
    {
        Seed(12);

        using var context = CreateContext();

        var page = await context.Notes
            .Select(note => new NoteRow
            {
                Id = note.Id,
                Title = note.Title,
                CreatedAt = note.CreatedAt,
            })
            .ToPagedResponseAsync(new CursorPageRequest { Limit = 5 });

        Assert.Equal(5, page.Value.Items.Count);
        Assert.Equal("note 011", page.Value.Items[0].Title);
        Assert.True(page.Value.HasNext);
    }

    [Fact]
    public async Task A_page_maps_to_the_response_shape_after_it_is_read()
    {
        Seed(12);

        using var context = CreateContext();

        var page = await context.Notes.ToPagedResponseAsync(new CursorPageRequest { Limit = 5 });

        // The response record takes its values through a constructor, which the provider cannot
        // order by — so it is built after the page comes back, not inside the query.
        var mapped = page.Value.Map(note => new NoteItem(note.Id, note.Title.ToUpperInvariant()));

        Assert.Equal("NOTE 011", mapped.Items[0].Title);
        Assert.Equal(page.Value.Cursor, mapped.Cursor);
        Assert.True(mapped.HasNext);
    }

    private sealed record NoteItem(Guid Id, string Title);

    private sealed record NoteRow : ICursorPageable
    {
        public required Guid Id { get; init; }

        public required string Title { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }
    }

    private sealed class Note : ICursorPageable
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }
    }

    private sealed class PagingDbContext(DbContextOptions<PagingDbContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // SQLite has no date type and refuses to order by DateTimeOffset. The binary converter
            // is the documented way around it, and it keeps the ordering intact — so the keyset
            // predicate under test is the same one Postgres, which has timestamptz, would run.
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }
}
