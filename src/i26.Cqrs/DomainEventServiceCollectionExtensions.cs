using i26.Core.DomainEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace i26.Cqrs;

/// <summary>Registration of the domain event plumbing of an application.</summary>
public static class DomainEventServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="DomainEventQueue"/> and a dispatcher that runs the handlers in
    /// process, both scoped.
    /// </summary>
    /// <remarks>
    /// Neither is registered over one already there, so an application with its own
    /// <see cref="IDomainEventDispatcher"/> — a background queue, an outbox — registers it and
    /// keeps it. The handlers themselves come from <see cref="CqrsServiceCollectionExtensions.AddHandlers"/>.
    /// </remarks>
    public static IServiceCollection AddDomainEvents(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IDomainEventDispatcher, InProcessDomainEventDispatcher>();
        services.TryAddScoped<DomainEventQueue>();

        return services;
    }
}
