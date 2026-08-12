using Dapper;
using i26.Core.Ids;
using i26.Core.Pagination;
using i26.Dapper.Ids;
using i26.Dapper.Pagination;
using i26.Dapper.Tests.Ids;
using Microsoft.Data.Sqlite;

namespace i26.Dapper.Tests.Pagination;

/// <summary>
/// Paging a hand-written query whose tie-breaker is a typed id. The cursor's id travels back as a
/// parameter, so the type handler is what has to turn it into the prefixed text the column holds —
/// and the text sorts the same way the id does, which is what keeps the boundary exact.
/// </summary>
public sealed class TypedIdPagingTests : IDisposable
{
    private const string Sql = """SELECT c."Id", c."Title", c."CreatedAt" FROM courses c""";

    private static readonly DateTimeOffset Start = new(2026, 8, 11, 20, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;

    public TypedIdPagingTests()
    {
        SqliteTypeHandlers.Register();
        TypedIdDapperExtensions.AddTypedIdHandlers(typeof(TypedIdPagingTests).Assembly);

        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _connection.Execute(
            """
            CREATE TABLE courses (
                "Id"        TEXT NOT NULL PRIMARY KEY,
                "Title"     TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            """);
    }

    public void Dispose() => _connection.Dispose();

    private void Seed(int count, DateTimeOffset? sameInstant = null)
    {
        for (var index = 0; index < count; index++)
        {
            _connection.Execute(
                """INSERT INTO courses ("Id", "Title", "CreatedAt") VALUES (@Id, @Title, @CreatedAt)""",
                new
                {
                    Id = CourseId.New(),
                    Title = $"course {index:D3}",
                    CreatedAt = sameInstant ?? Start.AddSeconds(index),
                });
        }
    }

    private async Task<List<CourseRow>> ReadEveryPageAsync(int limit)
    {
        var rows = new List<CourseRow>();
        string? cursor = null;

        for (var page = 0; page < 100; page++)
        {
            var result = await _connection.ToPagedResponseAsync<CourseRow, CourseId>(
                Sql,
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

        var page = await _connection.ToPagedResponseAsync<CourseRow, CourseId>(
            Sql,
            new CursorPageRequest { Limit = 3 });

        Assert.Equal(
            ["course 004", "course 003", "course 002"],
            page.Value.Items.Select(course => course.Title));

        Assert.True(page.Value.HasNext);
    }

    [Fact]
    public async Task Walking_the_pages_reads_every_row_once()
    {
        Seed(31);

        var rows = await ReadEveryPageAsync(limit: 7);

        Assert.Equal(31, rows.Count);
        Assert.Equal(31, rows.Select(course => course.Id).Distinct().Count());
    }

    [Fact]
    public async Task Rows_sharing_an_instant_are_still_cut_cleanly()
    {
        // Only the id column keeps the boundary from drifting, and it is text with a prefix.
        Seed(25, sameInstant: Start);

        var rows = await ReadEveryPageAsync(limit: 6);

        Assert.Equal(25, rows.Count);
        Assert.Equal(25, rows.Select(course => course.Id).Distinct().Count());
        Assert.Equal(rows.OrderByDescending(course => course.Id).Select(course => course.Id), rows.Select(course => course.Id));
    }

    [Fact]
    public async Task The_cursor_carries_the_id_in_its_own_textual_form()
    {
        Seed(5);

        var page = await _connection.ToPagedResponseAsync<CourseRow, CourseId>(
            Sql,
            new CursorPageRequest { Limit = 3 });

        Assert.NotNull(page.Value.Cursor);
        Assert.True(Cursor.TryDecode<CourseId>(page.Value.Cursor, out _, out var id));
        Assert.Equal(page.Value.Items[^1].Id, id);
    }

    [Fact]
    public async Task A_cursor_holding_another_entity_s_id_is_a_validation_failure()
    {
        Seed(5);

        var page = await _connection.ToPagedResponseAsync<CourseRow, CourseId>(
            Sql,
            new CursorPageRequest { Cursor = Cursor.Encode(Start, TeacherId.New()) });

        Assert.True(page.IsFailure);
        Assert.Equal(PaginationErrors.InvalidCursor, page.Error);
    }

    private sealed record CourseRow : ICursorPageable<CourseId>
    {
        public required CourseId Id { get; init; }

        public required string Title { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }
    }
}
