using i26.Core.DomainEvents;
using Microsoft.EntityFrameworkCore;

namespace i26.EntityFrameworkCore.DomainEvents;

/// <summary>Wiring of the domain event interceptor into a context.</summary>
public static class DomainEventDbContextOptionsExtensions
{
    /// <summary>
    /// Collects the domain events of the entities this context saves into the
    /// <see cref="DomainEventQueue"/> of the current scope.
    /// </summary>
    /// <param name="builder">The options being built.</param>
    /// <param name="serviceProvider">The scope the context is being created in.</param>
    /// <param name="publishing">When to publish. Defaults to right after a successful save.</param>
    /// <exception cref="InvalidOperationException">
    /// There is no <see cref="DomainEventQueue"/> in the scope.
    /// </exception>
    /// <remarks>
    /// Called from the two-argument overload of <c>AddDbContext</c>, whose service provider is the
    /// scoped one. A pooled or factory-created context is built from the root provider instead,
    /// where a scoped queue cannot be resolved — those construct the
    /// <see cref="DomainEventInterceptor"/> themselves.
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

        return builder.AddInterceptors(new DomainEventInterceptor(queue, publishing));
    }
}
