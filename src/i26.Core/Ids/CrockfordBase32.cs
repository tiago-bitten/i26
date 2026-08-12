namespace i26.Core.Ids;

/// <summary>
/// Lowercase Crockford base32 encoding of 16 bytes into 26 characters, in the form used by TypeID.
/// </summary>
/// <remarks>
/// Order preserving: comparing the text ordinally gives the same answer as comparing the bytes
/// big-endian. Decoding is case-sensitive, so every id has exactly one textual form.
/// </remarks>
public static class CrockfordBase32
{
    /// <summary>Lowercase Crockford base32 alphabet, without <c>i</c>, <c>l</c>, <c>o</c> and <c>u</c>.</summary>
    public const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    /// <summary>Number of characters in the encoded form.</summary>
    public const int EncodedLength = 26;

    /// <summary>Number of bytes in the decoded form.</summary>
    public const int DecodedLength = 16;

    /// <summary>Marks a character outside the alphabet in the lookup table.</summary>
    private const byte Invalid = 0xFF;

    /// <summary>Highest value the first character can hold (only 3 of its 5 bits are used).</summary>
    private const byte MaxFirstCharacterValue = 7;

    /// <summary>
    /// Decoding table indexed by the ASCII code of the character. Derived from <see cref="Alphabet"/>
    /// so the two cannot drift apart.
    /// </summary>
    private static readonly byte[] DecodeMap = CreateDecodeMap();

    /// <summary>Encodes 16 bytes into a new 26-character string.</summary>
    /// <param name="source">Exactly <see cref="DecodedLength"/> bytes.</param>
    /// <returns>The textual representation, <see cref="EncodedLength"/> characters long.</returns>
    /// <exception cref="ArgumentException"><paramref name="source"/> is not 16 bytes long.</exception>
    public static string Encode(ReadOnlySpan<byte> source)
    {
        ThrowIfSourceLengthInvalid(source);

        Span<char> destination = stackalloc char[EncodedLength];
        EncodeCore(source, destination);
        return new string(destination);
    }

    /// <summary>Encodes 16 bytes straight into a caller-owned destination.</summary>
    /// <param name="source">Exactly <see cref="DecodedLength"/> bytes.</param>
    /// <param name="destination">Destination with room for at least <see cref="EncodedLength"/> characters.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> is not 16 bytes long, or <paramref name="destination"/> is too small.
    /// </exception>
    public static void Encode(ReadOnlySpan<byte> source, Span<char> destination)
    {
        ThrowIfSourceLengthInvalid(source);

        if (destination.Length < EncodedLength)
        {
            throw new ArgumentException(
                $"The destination needs room for at least {EncodedLength} characters, but has {destination.Length}.",
                nameof(destination));
        }

        EncodeCore(source, destination);
    }

    /// <summary>Tries to decode 26 characters into 16 bytes.</summary>
    /// <param name="source">Encoded text; must be exactly <see cref="EncodedLength"/> characters long.</param>
    /// <param name="destination">Destination with room for at least <see cref="DecodedLength"/> bytes.</param>
    /// <returns>
    /// <see langword="true"/> on success; <see langword="false"/> when the length is wrong, when any
    /// character falls outside the alphabet (uppercase included), or when the first character is
    /// greater than <c>7</c> — which would represent more than 128 bits.
    /// </returns>
    public static bool TryDecode(ReadOnlySpan<char> source, Span<byte> destination)
    {
        if (source.Length != EncodedLength || destination.Length < DecodedLength)
        {
            return false;
        }

        var first = Decode(source[0]);
        if (first > MaxFirstCharacterValue)
        {
            // Covers both an invalid character (0xFF) and a 128-bit overflow.
            return false;
        }

        // The first character contributes 3 significant bits; the other 25 contribute 5 each.
        int buffer = first;
        var bits = 3;
        var written = 0;

        for (var i = 1; i < EncodedLength; i++)
        {
            var value = Decode(source[i]);
            if (value == Invalid)
            {
                return false;
            }

            buffer = (buffer << 5) | value;
            bits += 5;

            if (bits < 8) continue;
            bits -= 8;
            destination[written++] = (byte)(buffer >> bits);
        }

        return true;
    }

    private static void EncodeCore(ReadOnlySpan<byte> source, Span<char> destination)
    {
        // Two leading zero bits round the payload up to the 130 bits of 26 five-bit groups.
        var buffer = 0;
        var bits = 2;
        var written = 0;

        for (var i = 0; i < DecodedLength; i++)
        {
            buffer = (buffer << 8) | source[i];
            bits += 8;

            while (bits >= 5)
            {
                bits -= 5;
                destination[written++] = Alphabet[(buffer >> bits) & 0x1F];
            }
        }
    }

    private static byte Decode(char value) =>
        value < DecodeMap.Length ? DecodeMap[value] : Invalid;

    private static void ThrowIfSourceLengthInvalid(ReadOnlySpan<byte> source)
    {
        if (source.Length != DecodedLength)
        {
            throw new ArgumentException(
                $"The source must be exactly {DecodedLength} bytes long, but is {source.Length}.",
                nameof(source));
        }
    }

    private static byte[] CreateDecodeMap()
    {
        var map = new byte[128];
        map.AsSpan().Fill(Invalid);

        for (var i = 0; i < Alphabet.Length; i++)
        {
            map[Alphabet[i]] = (byte)i;
        }

        return map;
    }
}
