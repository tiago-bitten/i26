using i26.Core.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace i26.EntityFrameworkCore.Queries;

/// <summary>Registration of the query execution seam.</summary>
public static class AsyncQueryServiceCollectionExtensions
{
    /// <summary>Registers the Entity Framework backend and the executor in front of it, singleton.</summary>
    /// <remarks>
    /// Does not replace an <see cref="IAsyncQueryExecutor"/> already registered. A second store adds
    /// its own backend the same way, and the executor picks per query.
    /// </remarks>
    public static IServiceCollection AddEfCoreAsyncQueries(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAsyncQueryBackend, EfCoreAsyncQueryBackend>());

        services.TryAddSingleton<IAsyncQueryExecutor, AsyncQueryExecutor>();

        return services;
    }
}
