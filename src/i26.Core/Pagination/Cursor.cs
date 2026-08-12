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
    /// <summary>Separator between the two halves of a timestamp cursor.</summary>
    private const char Separator = '_';

    /// <summary>Width of a <see cref="Guid"/> in <c>D</c> format.</summary>
    private const int GuidLength = 36;

    /// <summary>Encodes the position of the last row of a page ordered by creation instant.</summary>
    public static string Encode(DateTimeOffset createdAt, Guid id)
    {
        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{createdAt.ToUnixTimeMilliseconds()}{Separator}{id:D}");

        return ToBase64Url(payload);
    }

    /// <summary>Reads back a cursor written by <see cref="Encode"/>. Plain base64 is accepted too.</summary>
    public static bool TryDecode(string? cursor, out DateTimeOffset createdAt, out Guid id)
    {
        createdAt = default;
        id = default;

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

        if (!Guid.TryParseExact(payload.AsSpan(separator + 1), "D", out id))
        {
            return false;
        }

        createdAt = DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
        return true;
    }

    /// <summary>Encodes the position of a row ordered by an arbitrary key — a name, a title.</summary>
    /// <remarks>
    /// The id goes first at its fixed width of 36 characters and the key takes the rest, because
    /// there is no character a sort key is guaranteed not to contain.
    /// </remarks>
    public static string EncodeKeyed(string sortKey, Guid id)
    {
        ArgumentNullException.ThrowIfNull(sortKey);

        return ToBase64Url(id.ToString("D", CultureInfo.InvariantCulture) + sortKey);
    }

    /// <summary>Reads back a cursor written by <see cref="EncodeKeyed"/>.</summary>
    public static bool TryDecodeKeyed(string? cursor, out string sortKey, out Guid id)
    {
        sortKey = string.Empty;
        id = default;

        if (!TryFromBase64Url(cursor, out var payload) || payload.Length < GuidLength)
        {
            return false;
        }

        if (!Guid.TryParseExact(payload.AsSpan(0, GuidLength), "D", out id))
        {
            return false;
        }

        sortKey = payload[GuidLength..];
        return true;
    }

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
