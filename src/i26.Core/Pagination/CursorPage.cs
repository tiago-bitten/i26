namespace i26.Core.Pagination;

/// <summary>
/// Builds a <see cref="PagedResponse{T}"/> out of rows read one longer than the page.
/// </summary>
/// <remarks>
/// Shared by every store adapter, so the boundary rules — how the extra row is read, when a cursor
/// is handed out — are decided in one place rather than once per database.
/// </remarks>
public static class CursorPage
{
    /// <summary>Trims the extra row and builds the page around what is left.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="items">
    /// The rows read, up to <paramref name="limit"/> + 1 of them. Trimmed in place when the extra
    /// row came back.
    /// </param>
    /// <param name="limit">How many rows the page holds.</param>
    /// <param name="total">The total matching rows, when it was asked for.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is not positive.</exception>
    public static PagedResponse<T> From<T>(List<T> items, int limit, int? total = null)
        where T : ICursorPageable<Guid>
        => From<T, Guid>(items, limit, total);

    /// <summary>Trims the extra row and builds the page around what is left.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <typeparam name="TId">The tie-breaker's type.</typeparam>
    /// <param name="items">
    /// The rows read, up to <paramref name="limit"/> + 1 of them. Trimmed in place when the extra
    /// row came back.
    /// </param>
    /// <param name="limit">How many rows the page holds.</param>
    /// <param name="total">The total matching rows, when it was asked for.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is not positive.</exception>
    /// <remarks>
    /// A cursor comes back only when there is a next page. Handing one out on the last page invites
    /// a client to ask for a page that is always empty.
    /// </remarks>
    public static PagedResponse<T> From<T, TId>(List<T> items, int limit, int? total = null)
        where T : ICursorPageable<TId>
        where TId : IComparable<TId>, IParsable<TId>
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var hasNext = items.Count > limit;

        if (hasNext)
        {
            items.RemoveRange(limit, items.Count - limit);
        }

        string? cursor = null;

        if (hasNext && items.Count > 0)
        {
            var last = items[^1];
            cursor = Cursor.Encode(last.CreatedAt, last.Id);
        }

        return new PagedResponse<T>
        {
            Items = items,
            HasNext = hasNext,
            Cursor = cursor,
            Total = total,
        };
    }
}
