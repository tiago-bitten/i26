using System.Threading.Channels;
using i26.Core.DomainEvents;

namespace i26.Hosting.DomainEvents;

/// <summary>The queue between publishing an event and handling it.</summary>
internal sealed class DomainEventChannel
{
    private readonly Channel<IDomainEvent> _channel;

    public DomainEventChannel(BackgroundDomainEventOptions options)
    {
        _channel = options.Capacity is { } capacity
            ? Channel.CreateBounded<IDomainEvent>(new BoundedChannelOptions(capacity)
            {
                // Waiting rather than dropping: an event that is not handled is a change the rest of
                // the system never hears about, which is worse than a slow request.
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = options.Concurrency is 1,
            })
            : Channel.CreateUnbounded<IDomainEvent>(new UnboundedChannelOptions
            {
                SingleReader = options.Concurrency is 1,
            });
    }

    public ChannelWriter<IDomainEvent> Writer => _channel.Writer;

    public ChannelReader<IDomainEvent> Reader => _channel.Reader;
}
