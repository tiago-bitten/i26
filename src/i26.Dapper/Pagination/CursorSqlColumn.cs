using System.Text.RegularExpressions;

namespace i26.Dapper.Pagination;

/// <summary>
/// A column name that is about to be written into SQL by hand.
/// </summary>
/// <remarks>
/// The paging query has to name the ordering columns, and a parameter cannot stand in for an
/// identifier. So the name is concatenated — which is only safe while it comes from your code. The
/// check here is a seatbelt for that: it accepts a plain identifier, a quoted one, and a qualified
/// one, and refuses anything that could close the expression and continue the statement.
/// </remarks>
internal static partial class CursorSqlColumn
{
    /// <summary>Ensures a column name is safe to concatenate into a statement.</summary>
    /// <param name="column">The name to check.</param>
    /// <param name="parameterName">The argument being checked, for the exception.</param>
    /// <returns>The name.</returns>
    /// <exception cref="ArgumentException">The name is empty or holds something other than an identifier.</exception>
    internal static string Validate(string column, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(column, parameterName);

        if (!Pattern().IsMatch(column))
        {
            throw new ArgumentException(
                $"'{column}' is not a column name. It is written into the SQL as an identifier, so it " +
                "may only hold letters, digits, underscores and the quoting of your provider — " +
                "\"Created At\", [CreatedAt], `created_at`, schema.column. It is never user input.",
                parameterName);
        }

        return column;
    }

    [GeneratedRegex("""^[\p{L}\p{N}_."\[\]`$ ]+$""", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
