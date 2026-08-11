using System.Diagnostics.CodeAnalysis;

namespace i26.Core.Ids;

/// <summary>
/// Generic helpers for typed identifiers. All the creation, formatting, parsing and comparison
/// logic lives here, once — the types implementing <see cref="ITypedId{TSelf}"/> merely delegate
/// to it.
/// </summary>
public static class TypedId
{
    /// <summary>Character separating the prefix from the encoded suffix.</summary>
    public const char Separator = '_';

    /// <summary>Creates a new <typeparamref name="TId"/> backed by a fresh UUIDv7.</summary>
    /// <typeparam name="TId">The id type.</typeparam>
    /// <returns>A new id, sortable by creation instant.</returns>
    public static TId New<TId>()
        where TId : struct, ITypedId<TId>
        => TId.FromGuid(Uuid7.New());

    /// <summary>Formats the id as <c>{prefix}_{suffix}</c>.</summary>
    /// <typeparam name="TId">The id type.</typeparam>
    /// <param name="id">The id to format.</param>
    /// <returns>The canonical textual representation, using a single allocation.</returns>
    public static string Format<TId>(TId id)
        where TId : struct, ITypedId<TId>
    {
        var prefix = TypedIdPrefixCache<TId>.Value;

        return string.Create(
            GetFormattedLength(prefix),
            (Prefix: prefix, id.Value),
            static (destination, state) =>
            {
                state.Prefix.AsSpan().CopyTo(destination);
                destination[state.Prefix.Length] = Separator;

                Span<byte> bytes = stackalloc byte[CrockfordBase32.DecodedLength];
                state.Value.TryWriteBytes(bytes, bigEndian: true, out _);
                CrockfordBase32.Encode(bytes, destination[(state.Prefix.Length + 1)..]);
            });
    }

    /// <summary>Formats the id into a caller-owned buffer, allocating nothing.</summary>
    /// <typeparam name="TId">The id type.</typeparam>
    /// <param name="id">The id to format.</param>
    /// <param name="destination">Destination buffer.</param>
    /// <param name="charsWritten">Number of characters written; zero when this returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the id fit in <paramref name="destination"/>.</returns>
    public static bool TryFormat<TId>(TId id, Span<char> destination, out int charsWritten)
        where TId : struct, ITypedId<TId>
    {
        var prefix = TypedIdPrefixCache<TId>.Value;
        var length = GetFormattedLength(prefix);

        if (destination.Length < length)
        {
            charsWritten = 0;
            return false;
        }

        prefix.AsSpan().CopyTo(destination);
        destination[prefix.Length] = Separator;

        Span<byte> bytes = stackalloc byte[CrockfordBase32.DecodedLength];
        id.Value.TryWriteBytes(bytes, bigEndian: true, out _);
        CrockfordBase32.Encode(bytes, destination[(prefix.Length + 1)..]);

        charsWritten = length;
        return true;
    }

    /// <summary>Parses the textual representation of an id.</summary>
    /// <typeparam name="TId">The id type.</typeparam>
    /// <param name="s">The text to parse.</param>
    /// <returns>The matching id.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">
    /// The text is not a valid <typeparamref name="TId"/> — prefix other than the expected one,
    /// missing separator, wrong length, or a suffix outside the lowercase Crockford base32 alphabet.
    /// </exception>
    public static TId Parse<TId>(string? s)
        where TId : struct, ITypedId<TId>
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse<TId>(s.AsSpan());
    }

    /// <summary>Parses the textual representation of an id.</summary>
    /// <typeparam name="TId">The id type.</typeparam>
    /// <param name="s">The text to parse.</param>
    /// <returns>The matching id.</returns>
    /// <exception cref="FormatException">The text is not a valid <typeparamref name="TId"/>.</exception>
    public static TId Parse<TId>(ReadOnlySpan<char> s)
        where TId : struct, ITypedId<TId>
    {
        if (!TryParse<TId>(s, out var result))
        {
            var prefix = TypedIdPrefixCache<TId>.Value;

            throw new FormatException(
                $"'{s.ToString()}' is not a valid {typeof(TId).Name}. Expected '{prefix}{Separator}' " +
                $"followed by {CrockfordBase32.EncodedLength} characters of the lowercase Crockford " +
                $"base32 alphabet ('{CrockfordBase32.Alphabet}'), {GetFormattedLength(prefix)} characters in total.");
        }

        return result;
    }

    /// <summary>Tries to parse the textual representation of an id.</summary>
    /// <typeparam name="TId">The id type.</typeparam>
    /// <param name="s">The text to parse; may be <see langword="null"/>.</param>
    /// <param name="result">The parsed id, or <c>default</c> on failure.</param>
    /// <returns><see langword="true"/> when the text was a valid <typeparamref name="TId"/>.</returns>
    public static bool TryParse<TId>([NotNullWhen(true)] string? s, out TId result)
        where TId : struct, ITypedId<TId>
    {
        if (s is null)
        {
            result = default;
            return false;
        }

        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>Tries to parse the textual representation of an id.</summary>
    /// <typeparam name="TId">The id type.</typeparam>
    /// <param name="s">The text to parse.</param>
    /// <param name="result">The parsed id, or <c>default</c> on failure.</param>
    /// <returns><see langword="true"/> when the text was a valid <typeparamref name="TId"/>.</returns>
    public static bool TryParse<TId>(ReadOnlySpan<char> s, out TId result)
        where TId : struct, ITypedId<TId>
    {
        result = default;

        var prefix = TypedIdPrefixCache<TId>.Value;
        if (s.Length != GetFormattedLength(prefix))
        {
            return false;
        }

        // Ordinal comparison: the prefix is part of the type's identity, not user-facing text.
        if (!s[..prefix.Length].SequenceEqual(prefix.AsSpan()) || s[prefix.Length] != Separator)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[CrockfordBase32.DecodedLength];
        if (!CrockfordBase32.TryDecode(s[(prefix.Length + 1)..], bytes))
        {
            return false;
        }

        result = TId.FromGuid(new Guid(bytes, bigEndian: true));
        return true;
    }

    /// <summary>Compares two ids in chronological creation order.</summary>
    /// <typeparam name="TId">The id type.</typeparam>
    /// <param name="left">First id.</param>
    /// <param name="right">Second id.</param>
    /// <returns>Negative, zero or positive, per the <see cref="IComparable{T}.CompareTo"/> contract.</returns>
    /// <remarks>
    /// <para>
    /// Compares the UUID bytes in big-endian order, so the result matches an ordinal comparison of
    /// the formatted strings and, for UUIDv7, the creation order. That is also the order a
    /// <c>text COLLATE "C"</c> column uses, so sorting in the application and sorting in Postgres
    /// agree.
    /// </para>
    /// <para>
    /// <strong>Do not confuse this with the byte order of <see cref="Guid.ToByteArray()"/></strong>:
    /// that one is little-endian for the first three fields, so comparing those bytes scrambles the
    /// chronological order. The same applies to providers that sort GUIDs by their own rules, such
    /// as SQL Server's <c>uniqueidentifier</c>.
    /// </para>
    /// <para>
    /// <see cref="Guid.CompareTo(Guid)"/> currently agrees with this order on .NET — it compares the
    /// fields unsigned, in the order they appear in the textual form. <c>Compare</c> exists to make
    /// that guarantee explicit and independent of that implementation detail.
    /// </para>
    /// </remarks>
    public static int Compare<TId>(TId left, TId right)
        where TId : struct, ITypedId<TId>
    {
        Span<byte> leftBytes = stackalloc byte[CrockfordBase32.DecodedLength];
        Span<byte> rightBytes = stackalloc byte[CrockfordBase32.DecodedLength];

        left.Value.TryWriteBytes(leftBytes, bigEndian: true, out _);
        right.Value.TryWriteBytes(rightBytes, bigEndian: true, out _);

        return leftBytes.SequenceCompareTo(rightBytes);
    }

    /// <summary>Reads the creation instant embedded in the id's UUIDv7.</summary>
    /// <typeparam name="TId">The id type.</typeparam>
    /// <param name="id">The id.</param>
    /// <returns>The instant, in UTC, with millisecond precision.</returns>
    public static DateTimeOffset GetTimestamp<TId>(TId id)
        where TId : struct, ITypedId<TId>
        => Uuid7.GetTimestamp(id.Value);

    /// <summary>Exact length of the textual form of an id with the given prefix.</summary>
    /// <param name="prefix">The type's prefix.</param>
    /// <returns>Prefix plus separator plus encoded suffix.</returns>
    public static int GetFormattedLength(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return prefix.Length + 1 + CrockfordBase32.EncodedLength;
    }
}
