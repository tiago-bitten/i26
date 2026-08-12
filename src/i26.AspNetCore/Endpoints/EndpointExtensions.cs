using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace i26.AspNetCore.Endpoints;

/// <summary>Discovery and mapping of the <see cref="IEndpoint"/> implementations of an application.</summary>
public static class EndpointExtensions
{
    /// <summary>Registers every concrete <see cref="IEndpoint"/> found in the given assemblies, transient.</summary>
    /// <remarks>
    /// Abstract types, interfaces and open generics are skipped, and scanning the same assembly
    /// twice is harmless.
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
    /// Maps every registered <see cref="IEndpoint"/> onto the application, or onto the group this is
    /// called on, whose prefix and conventions they then inherit.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Nothing is registered, which means <see cref="AddEndpoints"/> was never called. Mapping no
    /// routes at all is never what was meant.
    /// </exception>
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
