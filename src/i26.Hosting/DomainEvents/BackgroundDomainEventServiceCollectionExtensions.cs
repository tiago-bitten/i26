using i26.Core.DomainEvents;
using i26.Cqrs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace i26.Hosting.DomainEvents;

/// <summary>Registration of background domain event handling.</summary>
public static class BackgroundDomainEventServiceCollectionExtensions
{
    /// <summary>Publishes domain events into an in-memory queue, handled by a hosted service.</summary>
    /// <remarks>
    /// Takes over <see cref="IDomainEventDispatcher"/>, in either call order with
    /// <c>AddDomainEvents</c>. The handlers still come from <c>AddHandlers</c>, and the queue is in
    /// memory: what it holds when the process ends is lost.
    /// </remarks>
    public static IServiceCollection AddBackgroundDomainEvents(
        this IServiceCollection services,
        Action<BackgroundDomainEventOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new BackgroundDomainEventOptions();
        configure?.Invoke(options);
        options.Validate();

        services.TryAddSingleton(options);
        services.TryAddSingleton<DomainEventChannel>();
        services.TryAddScoped<DomainEventQueue>();

        // What runs the handlers once the background service has a scope to run them in. Registered
        // as itself: the interface is taken by the dispatcher that does the queueing.
        services.TryAddScoped<InProcessDomainEventDispatcher>();

        services.RemoveAll<IDomainEventDispatcher>();
        services.AddSingleton<IDomainEventDispatcher, BackgroundDomainEventDispatcher>();

        services.AddHostedService<DomainEventBackgroundService>();

        return services;
    }
}
