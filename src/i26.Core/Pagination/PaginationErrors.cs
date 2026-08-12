using i26.Core.Results;

namespace i26.Core.Pagination;

/// <summary>
/// Failures the paging helpers answer with.
/// </summary>
public static class PaginationErrors
{
    /// <summary>
    /// The cursor did not come from this API — truncated, hand-written, or from another endpoint.
    /// </summary>
    /// <remarks>
    /// A validation failure, so it reaches the caller as a 400: the cursor is theirs to fix. The
    /// code follows the same <c>request.{field}.invalid</c> shape a malformed body gets.
    /// </remarks>
    public static readonly Error InvalidCursor = Error.Validation("request.cursor.invalid");
}
