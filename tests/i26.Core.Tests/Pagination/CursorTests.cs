using i26.Core.Ids;
using i26.Core.Pagination;
using i26.Core.Tests.Ids;

namespace i26.Core.Tests.Pagination;

public class CursorTests
{
    private static readonly DateTimeOffset CreatedAt =
        DateTimeOffset.FromUnixTimeMilliseconds(1_688_096_058_518);

    private static readonly Guid Id = Guid.Parse("01890a5d-ac96-774b-bcce-b302099a8057");

    [Fact]
    public void A_cursor_reads_back_the_position_it_was_written_from()
    {
        var cursor = Cursor.Encode(CreatedAt, Id);

        Assert.True(Cursor.TryDecode<Guid>(cursor, out var createdAt, out var id));
        Assert.Equal(CreatedAt, createdAt);
        Assert.Equal(Id, id);
    }

    [Fact]
    public void It_survives_a_query_string_without_escaping()
    {
        // Base64url: no +, / or = to be mangled by a client that forgets to encode.
        for (var i = 0; i < 500; i++)
        {
            var cursor = Cursor.Encode(CreatedAt.AddMilliseconds(i), Guid.NewGuid());

            Assert.DoesNotContain('+', cursor);
            Assert.DoesNotContain('/', cursor);
            Assert.DoesNotContain('=', cursor);
        }
    }

    [Fact]
    public void It_still_reads_a_cursor_written_in_plain_base64()
    {
        var payload = $"{CreatedAt.ToUnixTimeMilliseconds()}_{Id:D}";
        var standard = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));

        Assert.True(Cursor.TryDecode<Guid>(standard, out var createdAt, out var id));
        Assert.Equal(CreatedAt, createdAt);
        Assert.Equal(Id, id);
    }

    [Fact]
    public void Millisecond_precision_is_kept()
    {
        var precise = new DateTimeOffset(2026, 8, 11, 20, 45, 12, 345, TimeSpan.Zero);

        Assert.True(Cursor.TryDecode<Guid>(Cursor.Encode(precise, Id), out var createdAt, out _));
        Assert.Equal(precise, createdAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a cursor")]
    [InlineData("!!!!")]
    // Base64 of something that is not a cursor.
    [InlineData("aGVsbG8gd29ybGQ")]
    // The timestamp half is not a number.
    [InlineData("bm90YW51bWJlcl8wMTg5MGE1ZC1hYzk2LTc3NGItYmNjZS1iMzAyMDk5YTgwNTc")]
    // No separator.
    [InlineData("MTY4ODA5NjA1ODUxOA")]
    public void Anything_else_is_refused_instead_of_guessed(string? cursor)
    {
        Assert.False(Cursor.TryDecode<Guid>(cursor, out var createdAt, out var id));
        Assert.Equal(default, createdAt);
        Assert.Equal(Guid.Empty, id);
    }

    [Theory]
    // A long parses long before it names an instant, and a cursor comes from a query string.
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    [InlineData(253_402_300_800_000)]
    [InlineData(-62_135_596_800_001)]
    public void A_timestamp_no_instant_could_hold_is_refused_rather_than_thrown_on(long unixMilliseconds)
    {
        var payload = $"{unixMilliseconds}_{Id:D}";
        var cursor = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));

        Assert.False(Cursor.TryDecode<Guid>(cursor, out var createdAt, out _));
        Assert.Equal(default, createdAt);
    }

    [Theory]
    [InlineData(253_402_300_799_999)]
    [InlineData(-62_135_596_800_000)]
    public void The_ends_of_the_range_are_still_read(long unixMilliseconds)
    {
        var payload = $"{unixMilliseconds}_{Id:D}";
        var cursor = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));

        Assert.True(Cursor.TryDecode<Guid>(cursor, out var createdAt, out _));
        Assert.Equal(unixMilliseconds, createdAt.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void A_typed_id_travels_in_the_cursor_as_itself()
    {
        var id = TestUserId.New();

        var cursor = Cursor.Encode(CreatedAt, id);

        Assert.True(Cursor.TryDecode<TestUserId>(cursor, out var createdAt, out var decoded));
        Assert.Equal(CreatedAt, createdAt);
        Assert.Equal(id, decoded);
    }

    [Fact]
    public void A_cursor_of_one_id_type_does_not_read_as_another()
    {
        var cursor = Cursor.Encode(CreatedAt, TestUserId.New());

        Assert.False(Cursor.TryDecode<TestOrderId>(cursor, out _, out _));
    }

    [Theory]
    [InlineData("Ada Lovelace")]
    [InlineData("")]
    // A sort key is free to hold whatever a separator would have been.
    [InlineData("under_score")]
    [InlineData("a/b+c=d")]
    [InlineData("Ana Júlia Gonçalves")]
    [InlineData("日本語")]
    public void A_keyed_cursor_reads_back_any_sort_key(string sortKey)
    {
        var cursor = Cursor.EncodeKeyed(sortKey, Id);

        Assert.True(Cursor.TryDecodeKeyed<Guid>(cursor, out var decoded, out var id));
        Assert.Equal(sortKey, decoded);
        Assert.Equal(Id, id);
    }

    [Theory]
    [InlineData("Ada Lovelace")]
    [InlineData("")]
    [InlineData("under_score")]
    public void A_keyed_cursor_reads_back_a_typed_id_too(string sortKey)
    {
        var id = TestUserId.New();

        var cursor = Cursor.EncodeKeyed(sortKey, id);

        Assert.True(Cursor.TryDecodeKeyed<TestUserId>(cursor, out var decoded, out var decodedId));
        Assert.Equal(sortKey, decoded);
        Assert.Equal(id, decodedId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("dG9vc2hvcnQ")]
    // A length longer than what is left of the payload.
    [InlineData("OTk5XzAxODkwYTVk")]
    // A length that is not digits.
    [InlineData("LTFfMDE4OTBhNWQtYWM5Ni03NzRiLWJjY2UtYjMwMjA5OWE4MDU3")]
    public void A_keyed_cursor_that_cannot_hold_an_id_is_refused(string? cursor)
    {
        Assert.False(Cursor.TryDecodeKeyed<Guid>(cursor, out var sortKey, out var id));
        Assert.Empty(sortKey);
        Assert.Equal(Guid.Empty, id);
    }

    [Fact]
    public void The_two_kinds_of_cursor_do_not_read_each_other()
    {
        Assert.False(Cursor.TryDecode<Guid>(Cursor.EncodeKeyed("Ada Lovelace", Id), out _, out _));
    }
}
