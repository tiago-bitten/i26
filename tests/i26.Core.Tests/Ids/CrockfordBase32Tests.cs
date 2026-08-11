using System.Security.Cryptography;
using i26.Core.Ids;

namespace i26.Core.Tests.Ids;

public class CrockfordBase32Tests
{
    [Fact]
    public void Encode_and_TryDecode_roundtrip_random_bytes()
    {
        Span<byte> original = stackalloc byte[CrockfordBase32.DecodedLength];
        Span<byte> decoded = stackalloc byte[CrockfordBase32.DecodedLength];

        for (var i = 0; i < 1_000; i++)
        {
            RandomNumberGenerator.Fill(original);

            var encoded = CrockfordBase32.Encode(original);

            Assert.Equal(CrockfordBase32.EncodedLength, encoded.Length);
            Assert.True(CrockfordBase32.TryDecode(encoded, decoded));
            Assert.True(original.SequenceEqual(decoded));
        }
    }

    [Fact]
    public void Encode_only_emits_alphabet_characters()
    {
        Span<byte> bytes = stackalloc byte[CrockfordBase32.DecodedLength];
        RandomNumberGenerator.Fill(bytes);

        foreach (var character in CrockfordBase32.Encode(bytes))
        {
            Assert.Contains(character, CrockfordBase32.Alphabet);
        }
    }

    [Fact]
    public void Alphabet_excludes_the_ambiguous_characters()
    {
        Assert.Equal(32, CrockfordBase32.Alphabet.Length);
        Assert.Equal(32, CrockfordBase32.Alphabet.Distinct().Count());

        foreach (var ambiguous in "ilou")
        {
            Assert.DoesNotContain(ambiguous, CrockfordBase32.Alphabet);
        }
    }

    [Fact]
    public void Encode_of_zeroed_bytes_is_the_lowest_sequence()
    {
        Span<byte> zeros = stackalloc byte[CrockfordBase32.DecodedLength];

        Assert.Equal(new string('0', CrockfordBase32.EncodedLength), CrockfordBase32.Encode(zeros));
    }

    [Fact]
    public void Encode_of_the_highest_value_starts_with_7()
    {
        Span<byte> ones = stackalloc byte[CrockfordBase32.DecodedLength];
        ones.Fill(0xFF);

        // 128 bits across 26 five-bit groups leave 2 spare bits, so the first character tops out at 7.
        Assert.Equal("7" + new string('z', CrockfordBase32.EncodedLength - 1), CrockfordBase32.Encode(ones));
    }

    [Fact]
    public void Encode_preserves_byte_order()
    {
        Span<byte> smaller = stackalloc byte[CrockfordBase32.DecodedLength];
        Span<byte> larger = stackalloc byte[CrockfordBase32.DecodedLength];

        for (var index = 0; index < CrockfordBase32.DecodedLength; index++)
        {
            smaller.Clear();
            larger.Clear();
            smaller[index] = 0x01;
            larger[index] = 0x02;

            Assert.True(string.CompareOrdinal(
                CrockfordBase32.Encode(smaller),
                CrockfordBase32.Encode(larger)) < 0);
        }
    }

    [Fact]
    public void Encode_requires_exactly_16_bytes()
    {
        Assert.Throws<ArgumentException>(() => CrockfordBase32.Encode(new byte[15]));
        Assert.Throws<ArgumentException>(() => CrockfordBase32.Encode(new byte[17]));
    }

    [Fact]
    public void Encode_into_a_destination_requires_enough_room()
    {
        var source = new byte[CrockfordBase32.DecodedLength];
        var destination = new char[CrockfordBase32.EncodedLength - 1];

        Assert.Throws<ArgumentException>(() => CrockfordBase32.Encode(source, destination));
    }

    [Theory]
    // Too short.
    [InlineData("01h455vb4pex5vsknk084sn02")]
    // Too long.
    [InlineData("01h455vb4pex5vsknk084sn02qq")]
    // Uppercase is rejected: there is only one canonical representation.
    [InlineData("01H455VB4PEX5VSKNK084SN02Q")]
    // Ambiguous characters, outside the alphabet.
    [InlineData("01h455vb4pex5vsknk084sn02i")]
    [InlineData("01h455vb4pex5vsknk084sn02l")]
    [InlineData("01h455vb4pex5vsknk084sn02o")]
    [InlineData("01h455vb4pex5vsknk084sn02u")]
    // Outside ASCII.
    [InlineData("01h455vb4pex5vsknk084sn02ç")]
    // First character above 7: would represent more than 128 bits.
    [InlineData("81h455vb4pex5vsknk084sn02q")]
    [InlineData("z1h455vb4pex5vsknk084sn02q")]
    [InlineData("")]
    public void TryDecode_rejects_invalid_input(string encoded)
    {
        var destination = new byte[CrockfordBase32.DecodedLength];

        Assert.False(CrockfordBase32.TryDecode(encoded, destination));
    }

    [Fact]
    public void TryDecode_rejects_a_destination_that_is_too_small()
    {
        var destination = new byte[CrockfordBase32.DecodedLength - 1];

        Assert.False(CrockfordBase32.TryDecode("01h455vb4pex5vsknk084sn02q", destination));
    }

    [Theory]
    // Vectors from the TypeID spec.
    [InlineData("00000000000000000000000000", "00000000-0000-0000-0000-000000000000")]
    [InlineData("00000000000000000000000001", "00000000-0000-0000-0000-000000000001")]
    [InlineData("7zzzzzzzzzzzzzzzzzzzzzzzzz", "ffffffff-ffff-ffff-ffff-ffffffffffff")]
    [InlineData("01h455vb4pex5vsknk084sn02q", "01890a5d-ac96-774b-bcce-b302099a8057")]
    public void Encoding_matches_the_spec_vectors(string encoded, string guid)
    {
        var expected = new Guid(guid);

        Span<byte> bytes = stackalloc byte[CrockfordBase32.DecodedLength];
        expected.TryWriteBytes(bytes, bigEndian: true, out _);

        Assert.Equal(encoded, CrockfordBase32.Encode(bytes));

        Span<byte> decoded = stackalloc byte[CrockfordBase32.DecodedLength];
        Assert.True(CrockfordBase32.TryDecode(encoded, decoded));
        Assert.Equal(expected, new Guid(decoded, bigEndian: true));
    }
}
