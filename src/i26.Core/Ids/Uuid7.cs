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

    /// <summary>Bytes the timestamp occupies, at the front.</summary>
    private const int TimestampByteCount = 6;

    /// <summary>Index of the byte whose high nibble holds the version.</summary>
    private const int VersionByte = 6;

    /// <summary>The version nibble of a UUIDv7, in place.</summary>
    private const byte Version7 = 0x70;

    /// <summary>Mask of the version nibble.</summary>
    private const byte VersionMask = 0xF0;

    /// <summary>
    /// Largest instant a <see cref="DateTimeOffset"/> holds. The 48 timestamp bits reach the year
    /// 10889, so a value that is well-formed can still be out of range here.
    /// </summary>
    private static readonly long MaxUnixMilliseconds = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();

    /// <summary>Creates a new UUID version 7 from the current UTC clock.</summary>
    /// <returns>A version 7, RFC 4122 variant <see cref="Guid"/>.</returns>
    public static Guid New()
    {
#if NET9_0_OR_GREATER
        return Guid.CreateVersion7();
#else
        Span<byte> bytes = stackalloc byte[ByteCount];

        // Only the bytes the timestamp does not claim: the first six are written over below, and
        // asking the CSPRNG for them would be work thrown away.
        RandomNumberGenerator.Fill(bytes[TimestampByteCount..]);

        var unixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Bytes 0-5: 48-bit timestamp, big-endian.
        bytes[0] = (byte)(unixMilliseconds >> 40);
        bytes[1] = (byte)(unixMilliseconds >> 32);
        bytes[2] = (byte)(unixMilliseconds >> 24);
        bytes[3] = (byte)(unixMilliseconds >> 16);
        bytes[4] = (byte)(unixMilliseconds >> 8);
        bytes[5] = (byte)unixMilliseconds;

        // Byte 6, high nibble: version 7.
        bytes[VersionByte] = (byte)((bytes[VersionByte] & 0x0F) | Version7);

        // Byte 8, two high bits: RFC 4122 variant (10xx).
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes, bigEndian: true);
#endif
    }

    /// <summary>Reads the creation instant embedded in a UUID version 7.</summary>
    /// <param name="value">The UUID to read the timestamp from.</param>
    /// <returns>The instant, in UTC, with millisecond precision.</returns>
    /// <exception cref="ArgumentException">
    /// The value is not version 7, or its 48 timestamp bits land outside the range a
    /// <see cref="DateTimeOffset"/> can hold.
    /// </exception>
    /// <remarks>
    /// Use <see cref="TryGetTimestamp"/> for a value that came from outside: an id is parsed from
    /// its 128 bits alone, so any of them can arrive here.
    /// </remarks>
    public static DateTimeOffset GetTimestamp(Guid value)
    {
        if (!TryGetTimestamp(value, out var timestamp))
        {
            throw new ArgumentException(
                $"'{value}' is not a version 7 UUID with a readable timestamp, so its leading 48 bits " +
                "do not say when it was created.",
                nameof(value));
        }

        return timestamp;
    }

    /// <summary>Tries to read the creation instant embedded in a UUID version 7.</summary>
    /// <param name="value">The UUID to read the timestamp from.</param>
    /// <param name="timestamp">The instant, in UTC, with millisecond precision.</param>
    /// <returns>
    /// <see langword="false"/> when the value is not version 7, or when its timestamp is out of the
    /// range a <see cref="DateTimeOffset"/> holds.
    /// </returns>
    public static bool TryGetTimestamp(Guid value, out DateTimeOffset timestamp)
    {
        timestamp = default;

        Span<byte> bytes = stackalloc byte[ByteCount];
        value.TryWriteBytes(bytes, bigEndian: true, out _);

        // The version nibble is the only thing saying the leading 48 bits are a timestamp at all.
        // Without this check a UUIDv4 reads as an instant somewhere in the next eight millennia.
        if ((bytes[VersionByte] & VersionMask) != Version7)
        {
            return false;
        }

        var unixMilliseconds =
            ((long)bytes[0] << 40) |
            ((long)bytes[1] << 32) |
            ((long)bytes[2] << 24) |
            ((long)bytes[3] << 16) |
            ((long)bytes[4] << 8) |
            bytes[5];

        if (unixMilliseconds > MaxUnixMilliseconds)
        {
            return false;
        }

        timestamp = DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
        return true;
    }
}
