using System.Globalization;
using Dapper;
using i26.Core.Pagination;
using i26.Dapper.Pagination;
using Microsoft.Data.Sqlite;

namespace i26.Dapper.Tests.Pagination;

/// <summary>
/// Paging a hand-written query against a real database, the way a screen that outgrew the ORM does
/// it. Timestamps are stored as ISO-8601 UTC text, which SQLite orders lexicographically — the same
/// order a Postgres <c>timestamptz</c> column would give.
/// </summary>
public sealed class DapperPagingExtensionsTests : IDisposable
{
    private const string Sql = """SELECT n."Id", n."Title", n."CreatedAt" FROM notes n WHERE n."Archived" = 0""";

    private static readonly DateTimeOffset Start = new(2026, 8, 11, 20, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;

    public DapperPagingExtensionsTests()
    {
        SqliteTypeHandlers.Register();

        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _connection.Execute(
            """
            CREATE TABLE notes (
                "Id"        TEXT NOT NULL PRIMARY KEY,
                "Title"     TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "Archived"  INTEGER NOT NULL DEFAULT 0
            );
            """);
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>Seeds <paramref name="count"/> notes, one second apart, newest last.</summary>
    private void Seed(int count, bool archived = false, DateTimeOffset? sameInstant = null)
    {
        for (var index = 0; index < count; index++)
        {
            _connection.Execute(
                """INSERT INTO notes ("Id", "Title", "CreatedAt", "Archived") VALUES (@Id, @Title, @CreatedAt, @Archived)""",
                new
                {
                    Id = Guid.NewGuid(),
                    Title = $"note {index:D3}",
                    CreatedAt = sameInstant ?? Start.AddSeconds(index),
                    Archived = archived ? 1 : 0,
                });
        }
    }

    private static string Iso(DateTimeOffset value) => SqliteTypeHandlers.ToText(value);

    private Task<i26.Core.Results.Result<PagedResponse<NoteRow>>> PageAsync(
        CursorPageRequest request,
        int maxLimit = CursorPageRequest.DefaultMaxLimit) =>
        _connection.ToPagedResponseAsync<NoteRow>(Sql, request, maxLimit: maxLimit);

    /// <summary>Walks every page and returns the rows in the order they came out.</summary>
    private async Task<List<NoteRow>> ReadEveryPageAsync(int limit)
    {
        var rows = new List<NoteRow>();
        string? cursor = null;

        for (var page = 0; page < 100; page++)
        {
            var result = await PageAsync(new CursorPageRequest { Limit = limit, Cursor = cursor });

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

        var page = await PageAsync(new CursorPageRequest { Limit = 3 });

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
        Assert.Equal(
            rows.OrderByDescending(note => note.CreatedAt).Select(note => note.Title),
            rows.Select(note => note.Title));
    }

    [Fact]
    public async Task Rows_sharing_an_instant_are_still_cut_cleanly()
    {
        Seed(25, sameInstant: Start);

        var rows = await ReadEveryPageAsync(limit: 7);

        Assert.Equal(25, rows.Count);
        Assert.Equal(25, rows.Select(note => note.Id).Distinct().Count());
    }

    [Fact]
    public async Task The_filter_of_the_query_is_kept_across_pages()
    {
        Seed(10);
        Seed(40, archived: true);

        var rows = await ReadEveryPageAsync(limit: 4);

        Assert.Equal(10, rows.Count);
    }

    [Fact]
    public async Task The_parameters_of_the_query_reach_the_database()
    {
        Seed(6);

        var page = await _connection.ToPagedResponseAsync<NoteRow>(
            """SELECT n."Id", n."Title", n."CreatedAt" FROM notes n WHERE n."Title" > @After""",
            new CursorPageRequest { Limit = 10, IncludeTotal = true },
            new { After = "note 003" });

        Assert.Equal(2, page.Value.Total);
        Assert.Equal(["note 005", "note 004"], page.Value.Items.Select(note => note.Title));
    }

    [Fact]
    public async Task The_last_page_hands_out_no_cursor()
    {
        Seed(3);

        var page = await PageAsync(new CursorPageRequest { Limit = 10 });

        Assert.Equal(3, page.Value.Items.Count);
        Assert.False(page.Value.HasNext);
        Assert.Null(page.Value.Cursor);
    }

    [Fact]
    public async Task An_empty_table_is_an_empty_page()
    {
        var page = await PageAsync(new CursorPageRequest { IncludeTotal = true });

        Assert.Empty(page.Value.Items);
        Assert.False(page.Value.HasNext);
        Assert.Equal(0, page.Value.Total);
    }

    [Fact]
    public async Task The_total_counts_the_whole_match_not_the_page()
    {
        Seed(30);

        var page = await PageAsync(new CursorPageRequest { Limit = 5, IncludeTotal = true });

        Assert.Equal(30, page.Value.Total);
        Assert.Equal(5, page.Value.Items.Count);
    }

    [Fact]
    public async Task The_total_is_left_out_unless_it_was_asked_for()
    {
        Seed(30);

        Assert.Null((await PageAsync(new CursorPageRequest { Limit = 5 })).Value.Total);
    }

    [Fact]
    public async Task A_cursor_that_did_not_come_from_here_is_a_validation_failure()
    {
        Seed(3);

        var page = await PageAsync(new CursorPageRequest { Cursor = "made up" });

        Assert.True(page.IsFailure);
        Assert.Equal(PaginationErrors.InvalidCursor, page.Error);
        Assert.Equal(400, page.StatusCode);
    }

    [Fact]
    public async Task The_limit_is_clamped_to_the_ceiling()
    {
        Seed(30);

        var page = await PageAsync(new CursorPageRequest { Limit = 1_000 }, maxLimit: 8);

        Assert.Equal(8, page.Value.Items.Count);
        Assert.True(page.Value.HasNext);
    }

    [Fact]
    public async Task A_cursor_from_the_orm_side_is_read_the_same_way()
    {
        Seed(20);

        // The two adapters share one cursor format, so a page can be read by either.
        var first = await PageAsync(new CursorPageRequest { Limit = 5 });
        var last = first.Value.Items[^1];

        var second = await PageAsync(new CursorPageRequest
        {
            Limit = 5,
            Cursor = Cursor.Encode(last.CreatedAt, last.Id),
        });

        Assert.Equal("note 014", second.Value.Items[0].Title);
    }

    [Theory]
    [InlineData("\"CreatedAt\"; DROP TABLE notes")]
    [InlineData("CreatedAt) OR (1=1")]
    [InlineData("")]
    [InlineData("  ")]
    public async Task A_column_name_that_is_not_one_is_refused(string column)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _connection.ToPagedResponseAsync<NoteRow>(
            Sql,
            new CursorPageRequest(),
            createdAtColumn: column));
    }

    [Fact]
    public async Task The_column_names_can_be_the_ones_the_query_uses()
    {
        _connection.Execute(
            """
            CREATE TABLE snake (id TEXT NOT NULL PRIMARY KEY, created_at TEXT NOT NULL);
            INSERT INTO snake (id, created_at) VALUES (@a, @t1), (@b, @t2);
            """,
            new
            {
                a = Guid.NewGuid().ToString("D"),
                b = Guid.NewGuid().ToString("D"),
                t1 = Iso(Start),
                t2 = Iso(Start.AddSeconds(1)),
            });

        var page = await _connection.ToPagedResponseAsync<NoteRow>(
            """SELECT s.id AS "Id", '' AS "Title", s.created_at AS "CreatedAt" FROM snake s""",
            new CursorPageRequest { Limit = 1 },
            createdAtColumn: "\"CreatedAt\"",
            idColumn: "\"Id\"");

        Assert.Single(page.Value.Items);
        Assert.True(page.Value.HasNext);
    }

    private sealed record NoteRow : ICursorPageable
    {
        public required Guid Id { get; init; }

        public required string Title { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }
    }
}
