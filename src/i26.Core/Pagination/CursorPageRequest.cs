namespace i26.Core.Pagination;

/// <summary>What a caller asks for: how many rows, and where the last page stopped.</summary>
/// <remarks>Meant to be inherited by the query that needs paging, so the two travel together.</remarks>
public record CursorPageRequest
{
    /// <summary>Rows per page when the caller does not say.</summary>
    public const int DefaultLimit = 10;

    /// <summary>Most rows a page will return, however many were asked for.</summary>
    public const int DefaultMaxLimit = 100;

    /// <summary>How many rows to return.</summary>
    public int Limit { get; init; } = DefaultLimit;

    /// <summary>Where the last page stopped; null for the first one.</summary>
    public string? Cursor { get; init; }

    /// <summary>Whether to also count every row the query matches.</summary>
    /// <remarks>
    /// Off by default, and worth leaving off: the count is a second query over the whole matching
    /// set, which is the cost cursor paging exists to avoid.
    /// </remarks>
    public bool IncludeTotal { get; init; }

    /// <summary>Brings <see cref="Limit"/> into <c>[1, maxLimit]</c>.</summary>
    /// <remarks>
    /// Clamped rather than refused: a page size is not worth a failed request, and the ceiling is
    /// what keeps one caller from asking the database for everything.
    /// </remarks>
    public CursorPageRequest Normalize(int maxLimit = DefaultMaxLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLimit);

        var limit = Math.Clamp(Limit, 1, maxLimit);

        return limit == Limit ? this : this with { Limit = limit };
    }
}
