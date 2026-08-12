namespace i26.Core.Queries;

/// <summary>An executor that speaks for one LINQ provider.</summary>
/// <remarks>
/// One per store. <see cref="AsyncQueryExecutor"/> picks between them, per query, so the application
/// layer keeps injecting one <see cref="IAsyncQueryExecutor"/>.
/// </remarks>
public interface IAsyncQueryBackend : IAsyncQueryExecutor
{
    /// <summary>Whether this backend can run the query asynchronously.</summary>
    /// <remarks>Answered from <see cref="IQueryable.Provider"/>, never from the row type.</remarks>
    bool CanExecute(IQueryable query);
}
