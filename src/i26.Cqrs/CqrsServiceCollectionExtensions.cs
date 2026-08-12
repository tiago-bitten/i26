using System.Reflection;
using i26.Core.DomainEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace i26.Cqrs;

/// <summary>Registration of the command, query and domain event handlers of an application.</summary>
public static class CqrsServiceCollectionExtensions
{
    /// <summary>
    /// Registers every command, query and domain event handler found in the given assemblies,
    /// scoped, under the interfaces it implements. Internal and private handlers included.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Two handlers answer the same command or query, which is refused rather than resolved to
    /// whichever was scanned last. A domain event takes as many handlers as it finds.
    /// </exception>
    /// <remarks>
    /// Called from wherever the application layer wires itself up. Scanning the same assembly twice
    /// is harmless.
    /// </remarks>
    public static IServiceCollection AddHandlers(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            foreach (var implementation in assembly.GetTypes())
            {
                if (implementation is not { IsClass: true, IsAbstract: false } ||
                    implementation.ContainsGenericParameters)
                {
                    continue;
                }

                foreach (var handled in implementation.GetInterfaces())
                {
                    if (IsHandler(handled))
                    {
                        Register(services, handled, implementation);
                    }
                }
            }
        }

        return services;
    }

    private static bool IsHandler(Type candidate)
    {
        if (!candidate.IsGenericType)
        {
            return false;
        }

        var definition = candidate.GetGenericTypeDefinition();

        return definition == typeof(ICommandHandler<>)
            || definition == typeof(ICommandHandler<,>)
            || definition == typeof(IQueryHandler<,>)
            || definition == typeof(IDomainEventHandler<>);
    }

    private static void Register(IServiceCollection services, Type handled, Type implementation)
    {
        if (handled.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
        {
            // Many handlers per event is the point of an event, so the rule below does not apply.
            // TryAddEnumerable keeps a second scan of the same assembly from registering it twice.
            services.TryAddEnumerable(ServiceDescriptor.Scoped(handled, implementation));
            return;
        }

        var registered = services.FirstOrDefault(descriptor => descriptor.ServiceType == handled);

        if (registered is null)
        {
            services.AddScoped(handled, implementation);
            return;
        }

        if (registered.ImplementationType == implementation)
        {
            // The same assembly scanned twice, or a handler reached through two interfaces.
            return;
        }

        throw new InvalidOperationException(
            $"{registered.ImplementationType?.Name ?? "A handler"} and {implementation.Name} both handle " +
            $"{Describe(handled)}. A request has exactly one handler, and the container would answer " +
            "with whichever was registered last.");
    }

    private static string Describe(Type handled)
    {
        var request = handled.GenericTypeArguments[0];

        return handled.GenericTypeArguments.Length == 1
            ? request.Name
            : $"{request.Name} -> {handled.GenericTypeArguments[1].Name}";
    }
}
