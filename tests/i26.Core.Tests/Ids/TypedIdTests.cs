using System.Text.RegularExpressions;
using i26.Core.Ids;

namespace i26.Core.Tests.Ids;

public class TypedIdTests
{
    private const string SuffixPattern = "[0-9a-hjkmnp-tv-z]{26}";

    [Fact]
    public void New_Format_and_Parse_roundtrip()
    {
        var id = TestUserId.New();

        var text = TypedId.Format(id);
        var parsed = TypedId.Parse<TestUserId>(text);

        Assert.Equal(id, parsed);
        Assert.Equal(id.Value, parsed.Value);
    }

    [Fact]
    public void ToString_uses_the_canonical_format()
    {
        var id = TestUserId.New();

        Assert.Equal(TypedId.Format(id), id.ToString());
    }

    [Fact]
    public void Parse_on_the_type_accepts_the_formatted_string()
    {
        var id = TestOrderId.New();

        Assert.Equal(id, TestOrderId.Parse(id.ToString()));
    }

    [Fact]
    public void Format_produces_the_prefix_the_separator_and_26_characters()
    {
        var text = TypedId.Format(TestUserId.New());

        Assert.Matches($"^usr_{SuffixPattern}$", text);
        Assert.Equal(3 + 1 + 26, text.Length);
        Assert.Equal(TypedId.GetFormattedLength(TestUserId.Prefix), text.Length);
    }

    [Fact]
    public void Format_of_ids_with_different_prefixes_changes_only_the_prefix()
    {
        var guid = Uuid7.New();

        var user = TypedId.Format(TestUserId.FromGuid(guid));
        var order = TypedId.Format(TestOrderId.FromGuid(guid));

        Assert.StartsWith("usr_", user, StringComparison.Ordinal);
        Assert.StartsWith("ord_", order, StringComparison.Ordinal);
        Assert.Equal(user["usr_".Length..], order["ord_".Length..]);
    }

    [Fact]
    public void TryFormat_writes_into_the_caller_buffer()
    {
        var id = TestUserId.New();
        Span<char> buffer = stackalloc char[64];

        Assert.True(TypedId.TryFormat(id, buffer, out var written));
        Assert.Equal(TypedId.Format(id), new string(buffer[..written]));
    }

    [Fact]
    public void TryFormat_returns_false_when_the_buffer_is_too_small()
    {
        var id = TestUserId.New();
        Span<char> buffer = stackalloc char[29];

        Assert.False(TypedId.TryFormat(id, buffer, out var written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void TryParse_accepts_a_valid_string()
    {
        var id = TestUserId.New();

        Assert.True(TypedId.TryParse<TestUserId>(id.ToString(), out var parsed));
        Assert.Equal(id, parsed);
    }

    [Fact]
    public void TryParse_rejects_null()
    {
        Assert.False(TypedId.TryParse<TestUserId>(null, out var parsed));
        Assert.Equal(default, parsed);
    }

    [Theory]
    // Wrong prefix.
    [InlineData("xyz_01h455vb4pex5vsknk084sn02q")]
    // Prefix of ANOTHER typed id.
    [InlineData("ord_01h455vb4pex5vsknk084sn02q")]
    // Uppercase prefix.
    [InlineData("USR_01h455vb4pex5vsknk084sn02q")]
    // Uppercase suffix.
    [InlineData("usr_01H455VB4PEX5VSKNK084SN02Q")]
    // Characters outside the Crockford alphabet.
    [InlineData("usr_01h455vb4pex5vsknk084sn02i")]
    [InlineData("usr_01h455vb4pex5vsknk084sn02l")]
    [InlineData("usr_01h455vb4pex5vsknk084sn02o")]
    [InlineData("usr_01h455vb4pex5vsknk084sn02u")]
    [InlineData("usr_01h455vb4pex5vsknk084sn02-")]
    // Suffix too short.
    [InlineData("usr_01h455vb4pex5vsknk084sn02")]
    // Suffix too long.
    [InlineData("usr_01h455vb4pex5vsknk084sn02qq")]
    // No separator.
    [InlineData("usr01h455vb4pex5vsknk084sn02q")]
    // Wrong separator.
    [InlineData("usr-01h455vb4pex5vsknk084sn02q")]
    // No prefix.
    [InlineData("01h455vb4pex5vsknk084sn02q")]
    [InlineData("_01h455vb4pex5vsknk084sn02q")]
    // 128-bit overflow: first character above 7.
    [InlineData("usr_81h455vb4pex5vsknk084sn02q")]
    // Garbage.
    [InlineData("")]
    [InlineData("usr_")]
    [InlineData("nothing like it")]
    public void TryParse_rejects_invalid_input(string text)
    {
        Assert.False(TypedId.TryParse<TestUserId>(text, out var parsed));
        Assert.Equal(default, parsed);
    }

    [Fact]
    public void Parse_throws_FormatException_naming_the_expected_prefix()
    {
        var exception = Assert.Throws<FormatException>(
            () => TypedId.Parse<TestUserId>("ord_01h455vb4pex5vsknk084sn02q"));

        Assert.Contains("usr_", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestUserId), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_throws_ArgumentNullException_for_null()
    {
        Assert.Throws<ArgumentNullException>(() => TypedId.Parse<TestUserId>(null));
    }

    [Fact]
    public void Parsing_across_id_types_fails_at_runtime()
    {
        // At compile time the types are already incompatible: no conversion exists between
        // TestUserId and TestOrderId, not even through the Guid without going through FromGuid.
        var user = TestUserId.New();

        Assert.Throws<FormatException>(() => TypedId.Parse<TestOrderId>(user.ToString()));
        Assert.False(TypedId.TryParse<TestOrderId>(user.ToString(), out _));
    }

    [Fact]
    public void Ids_of_different_types_sharing_a_Guid_are_not_equal()
    {
        var guid = Uuid7.New();

        var user = TestUserId.FromGuid(guid);
        var order = TestOrderId.FromGuid(guid);

        Assert.Equal(user.Value, order.Value);
        Assert.NotEqual(user.ToString(), order.ToString());

        // Comparing the two takes explicit boxing — there is no == operator between them.
        Assert.False(Equals(user, order));
    }

    [Fact]
    public void Ids_created_in_sequence_sort_lexicographically()
    {
        const int count = 10;
        var ids = new TestUserId[count];

        for (var i = 0; i < count; i++)
        {
            ids[i] = TestUserId.New();

            // Two milliseconds guarantee distinct timestamps: within a single millisecond the
            // remaining bits are random and no ordering is guaranteed.
            Thread.Sleep(2);
        }

        var formatted = Array.ConvertAll(ids, id => id.ToString());

        var sorted = (string[])formatted.Clone();
        Array.Sort(sorted, StringComparer.Ordinal);

        Assert.Equal(formatted, sorted);

        for (var i = 1; i < count; i++)
        {
            Assert.True(TypedId.Compare(ids[i - 1], ids[i]) < 0);
            Assert.True(string.CompareOrdinal(formatted[i - 1], formatted[i]) < 0);
        }
    }

    [Fact]
    public void Compare_returns_zero_for_the_same_id()
    {
        var id = TestUserId.New();

        Assert.Equal(0, TypedId.Compare(id, TestUserId.FromGuid(id.Value)));
    }

    [Fact]
    public void Compare_uses_big_endian_not_the_Guid_byte_order()
    {
        // Same bytes except for timestamp bytes 4-5: 0x0001 comes before 0x0100.
        var earlier = new Guid("01890a5d-0001-7000-8000-000000000000");
        var later = new Guid("01890a5d-0100-7000-8000-000000000000");

        Assert.True(Uuid7.GetTimestamp(earlier) < Uuid7.GetTimestamp(later));

        var left = TestUserId.FromGuid(earlier);
        var right = TestUserId.FromGuid(later);

        Assert.True(TypedId.Compare(left, right) < 0);
        Assert.True(string.CompareOrdinal(left.ToString(), right.ToString()) < 0);

        // Guid.ToByteArray() is little-endian for the first three fields, so comparing those bytes
        // flips the chronological order. That is why Compare writes them big-endian.
        Assert.True(earlier.ToByteArray().AsSpan().SequenceCompareTo(later.ToByteArray()) > 0);

        // Guid.CompareTo, on the other hand, agrees with our order on current .NET. Should it ever
        // stop agreeing, this is where we find out.
        Assert.True(earlier.CompareTo(later) < 0);
    }

    [Fact]
    public void GetTimestamp_returns_an_instant_close_to_now()
    {
        var before = DateTimeOffset.UtcNow;
        var id = TestUserId.New();
        var after = DateTimeOffset.UtcNow;

        var timestamp = TypedId.GetTimestamp(id);

        Assert.InRange(
            timestamp,
            before.AddSeconds(-5),
            after.AddSeconds(5));
    }

    [Fact]
    public void GetTimestamp_reads_the_instant_of_a_known_id()
    {
        // Canonical TypeID vector: the suffix decodes to 01890a5d-ac96-774b-bcce-b302099a8057.
        var id = TypedId.Parse<TestExternalAuthId>("auth_01h455vb4pex5vsknk084sn02q");

        Assert.Equal(new Guid("01890a5d-ac96-774b-bcce-b302099a8057"), id.Value);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeMilliseconds(0x01890A5DAC96),
            TypedId.GetTimestamp(id));
    }

    [Fact]
    public void Prefixes_are_lowercase_and_free_of_the_separator()
    {
        foreach (var prefix in new[] { TestUserId.Prefix, TestOrderId.Prefix, TestExternalAuthId.Prefix })
        {
            Assert.Equal(prefix.ToLowerInvariant(), prefix);
            Assert.DoesNotContain("_", prefix, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Format_matches_the_documented_regex_for_many_ids()
    {
        // A timeout on every regex, even one this simple: an unbounded match is a habit worth not having.
        var regex = new Regex($"^usr_{SuffixPattern}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

        for (var i = 0; i < 1_000; i++)
        {
            Assert.Matches(regex, TestUserId.New().ToString());
        }
    }
}
