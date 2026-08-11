using i26.Core.Ids;

namespace i26.Core.Tests.Ids;

public class Uuid7Tests
{
    [Fact]
    public void New_sets_version_7_and_the_RFC_variant()
    {
        Span<byte> bytes = stackalloc byte[16];

        for (var i = 0; i < 1_000; i++)
        {
            Uuid7.New().TryWriteBytes(bytes, bigEndian: true, out _);

            Assert.Equal(0x70, bytes[6] & 0xF0);
            Assert.Equal(0x80, bytes[8] & 0xC0);
        }
    }

    [Fact]
    public void New_does_not_repeat()
    {
        var generated = new HashSet<Guid>();

        for (var i = 0; i < 10_000; i++)
        {
            Assert.True(generated.Add(Uuid7.New()));
        }
    }

    [Fact]
    public void GetTimestamp_returns_the_creation_instant()
    {
        var before = DateTimeOffset.UtcNow;
        var value = Uuid7.New();
        var after = DateTimeOffset.UtcNow;

        var timestamp = Uuid7.GetTimestamp(value);

        Assert.InRange(timestamp, before.AddSeconds(-5), after.AddSeconds(5));
    }

    [Fact]
    public void GetTimestamp_reads_the_48_most_significant_bits()
    {
        Assert.Equal(
            DateTimeOffset.FromUnixTimeMilliseconds(0x01890A5DAC96),
            Uuid7.GetTimestamp(new Guid("01890a5d-ac96-774b-bcce-b302099a8057")));
    }

    [Fact]
    public void Values_created_in_different_milliseconds_sort_in_big_endian_order()
    {
        var first = Uuid7.New();
        Thread.Sleep(2);
        var second = Uuid7.New();

        Span<byte> firstBytes = stackalloc byte[16];
        Span<byte> secondBytes = stackalloc byte[16];
        first.TryWriteBytes(firstBytes, bigEndian: true, out _);
        second.TryWriteBytes(secondBytes, bigEndian: true, out _);

        Assert.True(firstBytes.SequenceCompareTo(secondBytes) < 0);
    }
}
