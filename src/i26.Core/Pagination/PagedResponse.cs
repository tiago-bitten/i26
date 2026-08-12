namespace i26.Core.Pagination;

/// <summary>
/// One page of rows, and where to pick up from.
/// </summary>
/// <typeparam name="T">The row type.</typeparam>
public sealed record PagedResponse<T>
{
    /// <summary>The rows of this page.</summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>Whether there is at least one more row after this page.</summary>
    /// <remarks>
    /// Known exactly, not guessed: the page is read one row longer than asked for, and the extra
    /// row — if it came back — is the answer. It is dropped before the page is returned.
    /// </remarks>
    public bool HasNext { get; init; }

    /// <summary>
    /// What to send back to ask for the next page; <see langword="null"/> when this is the last one.
    /// </summary>
    public string? Cursor { get; init; }

    /// <summary>
    /// How many rows the query matches in total, when
    /// <see cref="CursorPageRequest.IncludeTotal"/> was asked for; <see langword="null"/> otherwise.
    /// </summary>
    public int? Total { get; init; }

    /// <summary>An empty page.</summary>
    public static PagedResponse<T> Empty { get; } = new();

    /// <summary>Turns each row into something else, keeping the page around it.</summary>
    /// <typeparam name="TOut">The row type to produce.</typeparam>
    /// <param name="selector">How to turn one row into the other.</param>
    /// <returns>The same page, with the rows mapped.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// For the usual split between the row the database returns and the one the API answers with:
    /// the query pages over the projection it can index, and the handler maps it to the response.
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
