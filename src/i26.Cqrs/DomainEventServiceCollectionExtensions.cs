using i26.Core.DomainEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace i26.Cqrs;

/// <summary>Registration of the domain event plumbing of an application.</summary>
public static class DomainEventServiceCollectionExtensions
{
    /// <summary>Registers the queue and an in-process dispatcher, both scoped.</summary>
    /// <remarks>
    /// Neither replaces one already registered. The handlers come from
    /// <see cref="CqrsServiceCollectionExtensions.AddHandlers"/>.
    /// </remarks>
    public static IServiceCollection AddDomainEvents(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IDomainEventDispatcher, InProcessDomainEventDispatcher>();
        services.TryAddScoped<DomainEventQueue>();

        return services;
    }
}
