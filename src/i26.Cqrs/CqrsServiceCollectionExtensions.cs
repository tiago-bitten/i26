using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace i26.Cqrs;

/// <summary>
/// Registration of the command and query handlers of an application.
/// </summary>
public static class CqrsServiceCollectionExtensions
{
    /// <summary>
    /// Registers every command and query handler found in the given assemblies, scoped, under the
    /// handler interfaces it implements.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan; usually the one holding the application layer.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/>, <paramref name="assemblies"/>, or one of them, is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Two handlers answer the same command or query — see the remarks.
    /// </exception>
    /// <remarks>
    /// <para>Called from wherever the application layer wires itself up:</para>
    /// <code>
    /// public static class DependencyInjection
    /// {
    ///     public static IServiceCollection AddApplication(this IServiceCollection services)
    ///     {
    ///         services.AddHandlers(typeof(DependencyInjection).Assembly);
    ///         // validators, decorators, services…
    ///
    ///         return services;
    ///     }
    /// }
    /// </code>
    /// <para>
    /// Endpoints then ask for the handler of the exact command they mean, and the container has it:
    /// </para>
    /// <code>
    /// [FromServices] ICommandHandler&lt;PublishCourseCommand&gt; handler
    /// </code>
    /// <para>
    /// Internal and private handlers are registered too — a handler is an implementation detail of
    /// the application layer and has no reason to be public.
    /// </para>
    /// <para>
    /// Two handlers for one request is refused rather than silently resolved to whichever was
    /// scanned last: a command has exactly one handler, and the day someone copies a handler and
    /// forgets to change the command, this says so. Scanning the same assembly twice is harmless.
    /// </para>
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
            || definition == typeof(IQueryHandler<,>);
    }

    private static void Register(IServiceCollection services, Type handled, Type implementation)
    {
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
