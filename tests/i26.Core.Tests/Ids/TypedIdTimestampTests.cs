using i26.Core.Ids;

namespace i26.Core.Tests.Ids;

/// <summary>
/// Reading the instant out of an id that came from outside. Parsing checks the prefix and the
/// alphabet, never the 128 bits, so an id can be well formed and still carry bits that name no
/// instant — which used to reach <c>DateTimeOffset.FromUnixTimeMilliseconds</c> and throw out of
/// whatever was reading a route parameter.
/// </summary>
public class TypedIdTimestampTests
{
    /// <summary>Parseable, and every bit set: the 48 leading bits reach the year 10889.</summary>
    private const string EverythingSet = "usr_7zzzzzzzzzzzzzzzzzzzzzzzzz";

    [Fact]
    public void An_id_from_the_route_cannot_throw_out_of_the_timestamp()
    {
        Assert.True(TestUserId.TryParse(EverythingSet, null, out var id));

        Assert.False(TypedId.TryGetTimestamp(id, out var timestamp));
        Assert.Equal(default, timestamp);
    }

    [Fact]
    public void Asking_for_it_outright_says_what_is_wrong()
    {
        var id = TestUserId.Parse(EverythingSet);

        var exception = Assert.Throws<ArgumentException>(() => TypedId.GetTimestamp(id));

        Assert.Contains("version 7", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_UUIDv4_is_refused_rather_than_read_as_an_instant()
    {
        // Version 4: the leading bits are random, and reading them as a timestamp lands somewhere in
        // the next eight millennia.
        var id = TestUserId.FromGuid(Guid.NewGuid());

        Assert.False(TypedId.TryGetTimestamp(id, out _));
        Assert.False(Uuid7.TryGetTimestamp(Guid.NewGuid(), out _));
    }

    [Fact]
    public void The_zero_id_carries_no_instant_either()
    {
        Assert.False(TypedId.TryGetTimestamp(TypedId.Empty<TestUserId>(), out _));
    }

    [Fact]
    public void A_real_id_reads_back_the_instant_it_was_minted_at()
    {
        var id = TestUserId.New();

        Assert.True(TypedId.TryGetTimestamp(id, out var timestamp));
        Assert.Equal(TypedId.GetTimestamp(id), timestamp);
        Assert.InRange(
            timestamp,
            DateTimeOffset.UtcNow.AddSeconds(-5),
            DateTimeOffset.UtcNow.AddSeconds(5));
    }

    [Fact]
    public void The_version_is_what_is_checked_not_the_range_alone()
    {
        // Version 7, timestamp zero: 1970, which is odd but is what the bits say.
        var id = TestUserId.FromGuid(new Guid("00000000-0000-7000-8000-000000000000"));

        Assert.True(TypedId.TryGetTimestamp(id, out var timestamp));
        Assert.Equal(DateTimeOffset.UnixEpoch, timestamp);
    }

    [Fact]
    public void A_version_7_id_past_the_end_of_time_is_refused()
    {
        // Version 7 and the variant in place, but a 48-bit timestamp beyond the year 9999.
        var id = TestUserId.FromGuid(new Guid("ffffffff-ffff-7fff-8fff-ffffffffffff"));

        Assert.False(TypedId.TryGetTimestamp(id, out _));
    }
}
