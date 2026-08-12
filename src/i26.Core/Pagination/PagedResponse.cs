namespace i26.Core.Pagination;

/// <summary>One page of rows, and where to pick up from.</summary>
/// <typeparam name="T">The row type.</typeparam>
public sealed record PagedResponse<T>
{
    /// <summary>The rows of this page.</summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>Whether there is at least one more row after this page.</summary>
    /// <remarks>
    /// Known exactly, not guessed: the page is read one row longer than asked for, and that row is
    /// the answer.
    /// </remarks>
    public bool HasNext { get; init; }

    /// <summary>What to send back for the next page; null when this is the last one.</summary>
    public string? Cursor { get; init; }

    /// <summary>Total matching rows, when <see cref="CursorPageRequest.IncludeTotal"/> was asked for.</summary>
    public int? Total { get; init; }

    /// <summary>An empty page.</summary>
    public static PagedResponse<T> Empty { get; } = new();

    /// <summary>Turns each row into something else, keeping the page around it.</summary>
    /// <remarks>
    /// For the usual split between the row the database returns and the one the API answers with.
    /// </remarks>
    public PagedResponse<TOut> Map<TOut>(Func<T, TOut> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        var items = new TOut[Items.Count];

        for (var index = 0; index < items.Length; index++)
        {
            items[index] = selector(Items[index]);
        }

        return new PagedResponse<TOut>
        {
            Items = items,
            HasNext = HasNext,
            Cursor = Cursor,
            Total = Total,
        };
    }
}
