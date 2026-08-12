namespace i26.EntityFrameworkCore.Ids;

/// <summary>How a typed id column is declared.</summary>
/// <remarks>
/// Both parts are the provider's vocabulary, not Entity Framework's, so there is no value that
/// works everywhere. <see langword="null"/> leaves the decision to the provider's own mapping.
/// </remarks>
public sealed record TypedIdStorage
{
    /// <summary>
    /// Postgres: a <c>text</c> column collated <c>"C"</c>, which sorts byte by byte and so keeps the
    /// ids in creation order without depending on the server locale.
    /// </summary>
    public static TypedIdStorage Postgres { get; } = new();

    /// <summary>Whatever the provider maps a string to, with its default collation.</summary>
    /// <remarks>
    /// The starting point for SQL Server, MySQL or SQLite, where <c>text</c> and <c>"C"</c> mean
    /// something else or nothing at all. Ordering is then the column collation's business: without
    /// a binary one, a page boundary can land differently than the id order says it should.
    /// </remarks>
    public static TypedIdStorage ProviderDefault { get; } = new() { ColumnType = null, Collation = null };

    /// <summary>Column type the ids are stored as, or <see langword="null"/> for the provider's.</summary>
    public string? ColumnType { get; init; } = "text";

    /// <summary>Collation of the column, or <see langword="null"/> for the provider's.</summary>
    public string? Collation { get; init; } = "C";
}
