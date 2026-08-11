using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace i26.AspNetCore.Endpoints;

/// <summary>
/// Discovery and mapping of the <see cref="IEndpoint"/> implementations of an application.
/// </summary>
public static class EndpointExtensions
{
    /// <summary>
    /// Registers every concrete <see cref="IEndpoint"/> found in the given assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan; usually the one holding the endpoints.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="assemblies"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Abstract types, interfaces and open generics are skipped. Calling this twice for the same
    /// assembly is harmless: each implementation is registered once.
    /// <code>
    /// builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());
    /// </code>
    /// </remarks>
    public static IServiceCollection AddEndpoints(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            var descriptors = assembly.DefinedTypes
                .Where(IsEndpoint)
                .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
                .ToArray();

            services.TryAddEnumerable(descriptors);
        }

        return services;
    }

    /// <summary>
    /// Maps every registered <see cref="IEndpoint"/> onto the given builder.
    /// </summary>
    /// <param name="builder">
    /// Where to map: the application, or a route group, in which case every endpoint inherits the
    /// group's prefix and conventions.
    /// </param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// No <see cref="IEndpoint"/> is registered, which means
    /// <see cref="AddEndpoints"/> was never called — mapping nothing is never what was meant.
    /// </exception>
    /// <remarks>
    /// <code>
    /// var api = app.MapGroup("v{version:apiVersion}")
    ///     .WithApiVersionSet(versionSet)
    ///     .RequireRateLimiting(RateLimits.PerUser);
    ///
    /// api.MapEndpoints();
    /// </code>
    /// </remarks>
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var endpoints = builder.ServiceProvider.GetServices<IEndpoint>().ToArray();

        if (endpoints.Length == 0)
        {
            throw new InvalidOperationException(
                $"No {nameof(IEndpoint)} is registered. Call {nameof(AddEndpoints)} on the service " +
                "collection, passing the assemblies that hold the endpoints, before mapping them.");
        }

        foreach (var endpoint in endpoints)
        {
            endpoint.MapEndpoint(builder);
        }

        return builder;
    }

    private static bool IsEndpoint(TypeInfo type) =>
        type is { IsAbstract: false, IsInterface: false, ContainsGenericParameters: false }
        && type.IsAssignableTo(typeof(IEndpoint));
}
