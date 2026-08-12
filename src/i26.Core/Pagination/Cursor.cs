using System.Globalization;
using System.Text;

namespace i26.Core.Pagination;

/// <summary>The opaque marker a client sends back to ask for the next page.</summary>
/// <remarks>
/// It says where the last page stopped, not how far it got, which is what makes the next page an
/// index seek instead of an <c>OFFSET</c>. Base64url, so a query string cannot mangle it.
/// </remarks>
public static class Cursor
{
    /// <summary>Separator between the two halves of a cursor.</summary>
    private const char Separator = '_';

    /// <summary>Earliest instant a <see cref="DateTimeOffset"/> holds, in Unix milliseconds.</summary>
    private static readonly long MinUnixMilliseconds = DateTimeOffset.MinValue.ToUnixTimeMilliseconds();

    /// <summary>Latest instant a <see cref="DateTimeOffset"/> holds, in Unix milliseconds.</summary>
    private static readonly long MaxUnixMilliseconds = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();

    /// <summary>Encodes the position of the last row of a page ordered by creation instant.</summary>
    /// <typeparam name="TId">The tie-breaker's type.</typeparam>
    /// <param name="createdAt">The instant the page stopped at.</param>
    /// <param name="id">The id the page stopped at.</param>
    public static string Encode<TId>(DateTimeOffset createdAt, TId id)
        where TId : IParsable<TId>
    {
        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{createdAt.ToUnixTimeMilliseconds()}{Separator}{Text(id)}");

        return ToBase64Url(payload);
    }

    /// <summary>Reads back a cursor written by <see cref="Encode"/>. Plain base64 is accepted too.</summary>
    /// <typeparam name="TId">The tie-breaker's type.</typeparam>
    /// <param name="cursor">The cursor as the client sent it back.</param>
    /// <param name="createdAt">The instant the last page stopped at.</param>
    /// <param name="id">The id the last page stopped at.</param>
    /// <remarks>
    /// A cursor arrives from a query string, so every part of it is input: the instant is range
    /// checked before it becomes a <see cref="DateTimeOffset"/>, and the id has to parse as one.
    /// </remarks>
    public static bool TryDecode<TId>(string? cursor, out DateTimeOffset createdAt, out TId id)
        where TId : IParsable<TId>
    {
        createdAt = default;
        id = default!;

        if (!TryFromBase64Url(cursor, out var payload))
        {
            return false;
        }

        var separator = payload.IndexOf(Separator, StringComparison.Ordinal);

        if (separator <= 0)
        {
            return false;
        }

        if (!long.TryParse(
                payload.AsSpan(0, separator),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var unixMilliseconds))
        {
            return false;
        }

        // A long parses long before it names an instant: without this, a hand-written cursor turns
        // FromUnixTimeMilliseconds into an unhandled exception on a query string.
        if (unixMilliseconds < MinUnixMilliseconds || unixMilliseconds > MaxUnixMilliseconds)
        {
            return false;
        }

        // The id's own text may hold the separator — a typed id does — so it is everything after the
        // first one, and the timestamp before it is digits either way.
        if (!TId.TryParse(payload[(separator + 1)..], CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        createdAt = DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
        id = parsed;
        return true;
    }

    /// <summary>Encodes the position of a row ordered by an arbitrary key — a name, a title.</summary>
    /// <typeparam name="TId">The tie-breaker's type.</typeparam>
    /// <param name="sortKey">What the page is ordered by.</param>
    /// <param name="id">The id the page stopped at.</param>
    /// <remarks>
    /// The id is length-prefixed and the key takes the rest, because there is no character a sort
    /// key is guaranteed not to contain, and no width an id is guaranteed to have.
    /// </remarks>
    public static string EncodeKeyed<TId>(string sortKey, TId id)
        where TId : IParsable<TId>
    {
        ArgumentNullException.ThrowIfNull(sortKey);

        var text = Text(id);
        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{text.Length}{Separator}{text}{sortKey}");

        return ToBase64Url(payload);
    }

    /// <summary>Reads back a cursor written by <see cref="EncodeKeyed"/>.</summary>
    /// <typeparam name="TId">The tie-breaker's type.</typeparam>
    /// <param name="cursor">The cursor as the client sent it back.</param>
    /// <param name="sortKey">What the last page stopped at.</param>
    /// <param name="id">The id the last page stopped at.</param>
    public static bool TryDecodeKeyed<TId>(string? cursor, out string sortKey, out TId id)
        where TId : IParsable<TId>
    {
        sortKey = string.Empty;
        id = default!;

        if (!TryFromBase64Url(cursor, out var payload))
        {
            return false;
        }

        var separator = payload.IndexOf(Separator, StringComparison.Ordinal);

        if (separator <= 0)
        {
            return false;
        }

        // NumberStyles.None: a length is digits, so a sign or a space is a malformed cursor rather
        // than something to be lenient about.
        if (!int.TryParse(
                payload.AsSpan(0, separator),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var length)
            || payload.Length < separator + 1 + length)
        {
            return false;
        }

        if (!TId.TryParse(
                payload.Substring(separator + 1, length),
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return false;
        }

        sortKey = payload[(separator + 1 + length)..];
        id = parsed;
        return true;
    }

    /// <summary>The id in the textual form its own parser reads back.</summary>
    private static string Text<TId>(TId id)
        where TId : IParsable<TId>
        => id?.ToString() ?? string.Empty;

    private static string ToBase64Url(string payload)
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));

        return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static bool TryFromBase64Url(string? cursor, out string payload)
    {
        payload = string.Empty;

        if (string.IsNullOrEmpty(cursor))
        {
            return false;
        }

        var base64 = cursor.Replace('-', '+').Replace('_', '/');
        var padding = (4 - (base64.Length % 4)) % 4;

        if (padding == 3)
        {
            // Not a length base64 can produce.
            return false;
        }

        try
        {
            payload = Encoding.UTF8.GetString(Convert.FromBase64String(base64 + new string('=', padding)));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
