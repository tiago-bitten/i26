using System.Threading.Channels;
using i26.Core.DomainEvents;
using Microsoft.Extensions.Logging;

namespace i26.Hosting.DomainEvents;

/// <summary>Queues the events instead of handling them, and returns.</summary>
internal sealed class BackgroundDomainEventDispatcher(
    DomainEventChannel channel,
    ILogger<BackgroundDomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public async Task DispatchAsync(
        IReadOnlyList<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            try
            {
                await channel.Writer.WriteAsync(domainEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                // Raised during shutdown, after the queue stopped accepting. The queue is in memory
                // and this is the shape of that: what is not handled before the process ends is lost.
                logger.LogWarning(
                    "{DomainEvent} was dropped: the application is shutting down.",
                    domainEvent.GetType().Name);
            }
        }
    }
}
