using System.Security.Cryptography;

namespace i26.Core.Ids;

/// <summary>
/// Creates and reads UUID version 7 values (RFC 9562): a 48-bit Unix timestamp in milliseconds
/// followed by random bits.
/// </summary>
/// <remarks>
/// Because the creation instant sits in the most significant bits, two UUIDv7 values created in
/// different milliseconds sort chronologically when compared byte by byte in big-endian order —
/// which is exactly what <see cref="TypedId.Compare{TId}"/> does and what the encoding in
/// <see cref="CrockfordBase32"/> preserves in the textual form. Within a single millisecond there
/// is no ordering guarantee: the remaining bits are random.
/// </remarks>
public static class Uuid7
{
    /// <summary>Number of bytes in a UUID.</summary>
    private const int ByteCount = 16;

    /// <summary>Creates a new UUID version 7 from the current UTC clock.</summary>
    /// <returns>A version 7, RFC 4122 variant <see cref="Guid"/>.</returns>
    public static Guid New()
    {
#if NET9_0_OR_GREATER
        return Guid.CreateVersion7();
#else
        Span<byte> bytes = stackalloc byte[ByteCount];
        RandomNumberGenerator.Fill(bytes);

        var unixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Bytes 0-5: 48-bit timestamp, big-endian.
        bytes[0] = (byte)(unixMilliseconds >> 40);
        bytes[1] = (byte)(unixMilliseconds >> 32);
        bytes[2] = (byte)(unixMilliseconds >> 24);
        bytes[3] = (byte)(unixMilliseconds >> 16);
        bytes[4] = (byte)(unixMilliseconds >> 8);
        bytes[5] = (byte)unixMilliseconds;

        // Byte 6, high nibble: version 7.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70);

        // Byte 8, two high bits: RFC 4122 variant (10xx).
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes, bigEndian: true);
#endif
    }

    /// <summary>Reads the creation instant embedded in a UUID version 7.</summary>
    /// <param name="value">The UUID to read the timestamp from.</param>
    /// <returns>The instant, in UTC, with millisecond precision.</returns>
    /// <remarks>
    /// The version is not validated: called with a GUID that is not version 7, this reads the 48
    /// most significant bits as a timestamp, which carries no meaning whatsoever.
    /// </remarks>
    public static DateTimeOffset GetTimestamp(Guid value)
    {
        Span<byte> bytes = stackalloc byte[ByteCount];
        value.TryWriteBytes(bytes, bigEndian: true, out _);

        var unixMilliseconds =
            ((long)bytes[0] << 40) |
            ((long)bytes[1] << 32) |
            ((long)bytes[2] << 24) |
            ((long)bytes[3] << 16) |
            ((long)bytes[4] << 8) |
            bytes[5];

        return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
    }
}
