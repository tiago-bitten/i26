using System.Data;
using Dapper;
using i26.Core.Pagination;
using i26.Core.Results;

namespace i26.Dapper.Pagination;

/// <summary>Cursor paging over a Dapper query.</summary>
/// <remarks>
/// The same cursor and the same <see cref="PagedResponse{T}"/> as the Entity Framework side, so a
/// screen can move from one to the other without the client noticing.
/// </remarks>
public static class DapperPagingExtensions
{
    /// <summary>Column the page is ordered by, when the caller does not say.</summary>
    public const string DefaultCreatedAtColumn = "\"CreatedAt\"";

    /// <summary>Tie-breaking column, when the caller does not say.</summary>
    public const string DefaultIdColumn = "\"Id\"";

    private const string PageAlias = "_page";
    private const string CursorCreatedAtParameter = "__cursorCreatedAt";
    private const string CursorIdParameter = "__cursorId";

    /// <summary>Reads one page of a query, newest first.</summary>
    /// <returns>
    /// The page, or <see cref="PaginationErrors.InvalidCursor"/> when the cursor cannot be read.
    /// </returns>
    /// <exception cref="ArgumentException">A column name holds something other than an identifier.</exception>
    /// <remarks>
    /// The query arrives filtered, not ordered and without a limit; it is wrapped as a derived
    /// table, so it has to select the two ordering columns. Those column arguments are written into
    /// the statement as identifiers, since no parameter can stand in for one — they come from your
    /// code, never from a request. The paging clause is <c>LIMIT</c>, which Postgres, SQLite and
    /// MySQL take; on SQL Server, write the outer query yourself and build the page with
    /// <see cref="CursorPage.From{T}"/>.
    /// </remarks>
    public static async Task<Result<PagedResponse<T>>> ToPagedResponseAsync<T>(
        this IDbConnection connection,
        string sql,
        CursorPageRequest request,
        object? parameters = null,
        string createdAtColumn = DefaultCreatedAtColumn,
        string idColumn = DefaultIdColumn,
        int maxLimit = CursorPageRequest.DefaultMaxLimit,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where T : ICursorPageable
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(request);

        CursorSqlColumn.Validate(createdAtColumn, nameof(createdAtColumn));
        CursorSqlColumn.Validate(idColumn, nameof(idColumn));

        var page = request.Normalize(maxLimit);

        DateTimeOffset cursorCreatedAt = default;
        Guid cursorId = default;
        var paging = !string.IsNullOrEmpty(page.Cursor);

        if (paging && !Cursor.TryDecode(page.Cursor, out cursorCreatedAt, out cursorId))
        {
            return PaginationErrors.InvalidCursor;
        }

        // Counted before the cursor narrows anything: the total is of the whole matching set.
        int? total = page.IncludeTotal
            ? await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    $"SELECT COUNT(*) FROM ({sql}) AS {PageAlias}",
                    parameters,
                    transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false)
            : null;

        object? arguments = parameters;
        var where = string.Empty;

        if (paging)
        {
            var cursorArguments = new DynamicParameters(parameters);
            cursorArguments.Add(CursorCreatedAtParameter, cursorCreatedAt);
            cursorArguments.Add(CursorIdParameter, cursorId);
            arguments = cursorArguments;

            where = $"""
                WHERE {PageAlias}.{createdAtColumn} < @{CursorCreatedAtParameter}
                   OR ({PageAlias}.{createdAtColumn} = @{CursorCreatedAtParameter}
                       AND {PageAlias}.{idColumn} < @{CursorIdParameter})
                """;
        }

        // One row more than asked for: its presence is the answer to "is there a next page".
        var itemsSql = $"""
            SELECT * FROM ({sql}) AS {PageAlias}
            {where}
            ORDER BY {PageAlias}.{createdAtColumn} DESC, {PageAlias}.{idColumn} DESC
            LIMIT {page.Limit + 1}
            """;

        var items = await connection.QueryAsync<T>(new CommandDefinition(
                itemsSql,
                arguments,
                transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return CursorPage.From([.. items], page.Limit, total);
    }
}
