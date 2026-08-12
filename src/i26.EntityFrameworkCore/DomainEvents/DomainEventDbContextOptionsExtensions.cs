using i26.Core.DomainEvents;
using Microsoft.EntityFrameworkCore;

namespace i26.EntityFrameworkCore.DomainEvents;

/// <summary>Wiring of the domain event interceptor into a context.</summary>
public static class DomainEventDbContextOptionsExtensions
{
    /// <summary>Collects what this context saves into the given <see cref="DomainEventQueue"/>.</summary>
    /// <param name="builder">The options being built.</param>
    /// <param name="queue">The queue of the unit of work this context belongs to.</param>
    /// <param name="publishing">When to publish. Defaults to right after a successful save.</param>
    /// <remarks>
    /// The overload for a context built by hand — a test, a pooled context, a factory — where there
    /// is no scope to take the queue from.
    /// </remarks>
    public static DbContextOptionsBuilder UseDomainEvents(
        this DbContextOptionsBuilder builder,
        DomainEventQueue queue,
        DomainEventPublishing publishing = DomainEventPublishing.AfterSaveChanges)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(queue);

        return builder.AddInterceptors(new DomainEventInterceptor(queue, publishing));
    }

    /// <summary>Collects what this context saves into the <see cref="DomainEventQueue"/> of the scope.</summary>
    /// <param name="builder">The options being built.</param>
    /// <param name="serviceProvider">The scope the context is being created in.</param>
    /// <param name="publishing">When to publish. Defaults to right after a successful save.</param>
    /// <exception cref="InvalidOperationException">
    /// There is no <see cref="DomainEventQueue"/> in the scope.
    /// </exception>
    /// <remarks>
    /// Called from the two-argument <c>AddDbContext</c>, whose provider is the scoped one.
    /// </remarks>
    public static DbContextOptionsBuilder UseDomainEvents(
        this DbContextOptionsBuilder builder,
        IServiceProvider serviceProvider,
        DomainEventPublishing publishing = DomainEventPublishing.AfterSaveChanges)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var queue = serviceProvider.GetService(typeof(DomainEventQueue)) as DomainEventQueue
            ?? throw new InvalidOperationException(
                $"No {nameof(DomainEventQueue)} in this scope. Call AddDomainEvents() on the service " +
                "collection, and pass the scoped provider that AddDbContext<TContext>((provider, " +
                "options) => …) hands to its callback.");

        return builder.UseDomainEvents(queue, publishing);
    }
}
