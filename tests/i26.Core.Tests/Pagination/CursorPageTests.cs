using i26.Core.Pagination;

namespace i26.Core.Tests.Pagination;

public class CursorPageTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeMilliseconds(1_688_096_058_518);

    private static List<Row> Rows(int count) =>
        [.. Enumerable.Range(0, count).Select(index => new Row(Guid.NewGuid(), Start.AddSeconds(-index)))];

    [Fact]
    public void The_extra_row_answers_the_question_and_is_dropped()
    {
        var rows = Rows(11);
        var last = rows[9];

        var page = CursorPage.From(rows, limit: 10);

        Assert.Equal(10, page.Items.Count);
        Assert.True(page.HasNext);
        Assert.Equal(Cursor.Encode(last.CreatedAt, last.Id), page.Cursor);
    }

    [Fact]
    public void A_page_that_did_not_fill_is_the_last_one()
    {
        var page = CursorPage.From(Rows(4), limit: 10);

        Assert.Equal(4, page.Items.Count);
        Assert.False(page.HasNext);
        Assert.Null(page.Cursor);
    }

    [Fact]
    public void A_page_that_filled_exactly_is_the_last_one_too()
    {
        // Ten rows for a limit of ten means the eleventh was asked for and did not come back.
        var page = CursorPage.From(Rows(10), limit: 10);

        Assert.Equal(10, page.Items.Count);
        Assert.False(page.HasNext);
        Assert.Null(page.Cursor);
    }

    [Fact]
    public void An_empty_page_hands_out_no_cursor()
    {
        var page = CursorPage.From(Rows(0), limit: 10);

        Assert.Empty(page.Items);
        Assert.False(page.HasNext);
        Assert.Null(page.Cursor);
        Assert.Null(page.Total);
    }

    [Fact]
    public void The_total_is_carried_only_when_it_was_asked_for()
    {
        Assert.Equal(42, CursorPage.From(Rows(3), limit: 10, total: 42).Total);
        Assert.Null(CursorPage.From(Rows(3), limit: 10).Total);
    }

    [Fact]
    public void It_refuses_a_limit_that_cannot_hold_a_page()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CursorPage.From(Rows(1), limit: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CursorPage.From(Rows(1), limit: -1));
    }

    [Fact]
    public void More_than_one_extra_row_is_trimmed_all_the_same()
    {
        var page = CursorPage.From(Rows(25), limit: 10);

        Assert.Equal(10, page.Items.Count);
        Assert.True(page.HasNext);
    }

    [Fact]
    public void Map_keeps_the_page_around_the_rows()
    {
        var page = CursorPage.From(Rows(11), limit: 10, total: 99);

        var mapped = page.Map(row => row.Id.ToString());

        Assert.Equal(page.Items.Select(row => row.Id.ToString()), mapped.Items);
        Assert.Equal(page.HasNext, mapped.HasNext);
        Assert.Equal(page.Cursor, mapped.Cursor);
        Assert.Equal(99, mapped.Total);
    }

    [Fact]
    public void An_empty_page_is_available_without_building_one()
    {
        Assert.Empty(PagedResponse<Row>.Empty.Items);
        Assert.False(PagedResponse<Row>.Empty.HasNext);
    }

    [Theory]
    [InlineData(10, 100, 10)]
    [InlineData(0, 100, 1)]
    [InlineData(-5, 100, 1)]
    [InlineData(1_000, 100, 100)]
    [InlineData(50, 20, 20)]
    public void Normalize_brings_the_limit_into_range(int asked, int maxLimit, int expected)
    {
        var request = new CursorPageRequest { Limit = asked };

        Assert.Equal(expected, request.Normalize(maxLimit).Limit);
    }

    [Fact]
    public void Normalize_keeps_everything_else()
    {
        var request = new CursorPageRequest { Limit = 5_000, Cursor = "abc", IncludeTotal = true };

        var normalized = request.Normalize();

        Assert.Equal(CursorPageRequest.DefaultMaxLimit, normalized.Limit);
        Assert.Equal("abc", normalized.Cursor);
        Assert.True(normalized.IncludeTotal);
    }

    [Fact]
    public void A_request_asks_for_ten_rows_and_no_count_by_default()
    {
        var request = new CursorPageRequest();

        Assert.Equal(10, request.Limit);
        Assert.Null(request.Cursor);
        Assert.False(request.IncludeTotal);
    }

    private sealed record Row(Guid Id, DateTimeOffset CreatedAt) : ICursorPageable;
}
