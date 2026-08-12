using i26.Core.DomainEvents;
using i26.Cqrs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace i26.Hosting.DomainEvents;

/// <summary>Reads the queue and runs the handlers, each event in a scope of its own.</summary>
internal sealed class DomainEventBackgroundService(
    DomainEventChannel channel,
    IServiceScopeFactory scopes,
    BackgroundDomainEventOptions options,
    ILogger<DomainEventBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stopping completes the queue rather than cancelling the read, so the events already in it
        // are still handled — within whatever the host allows shutdown to take.
        await using var stopping = stoppingToken.Register(() => channel.Writer.TryComplete());

        var workers = new Task[options.Concurrency];

        for (var worker = 0; worker < workers.Length; worker++)
        {
            workers[worker] = ConsumeAsync();
        }

        await Task.WhenAll(workers).ConfigureAwait(false);
    }

    private async Task ConsumeAsync()
    {
        await foreach (var domainEvent in channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            await HandleAsync(domainEvent).ConfigureAwait(false);
        }
    }

    private async Task HandleAsync(IDomainEvent domainEvent)
    {
        // The scope that raised the event ended with the request. Handlers get one of their own,
        // which means a database context of their own and no user or tenant already resolved in it.
        await using var scope = scopes.CreateAsyncScope();

        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<InProcessDomainEventDispatcher>();

            // Not the stopping token: a handler cancelled halfway through leaves the same mess as
            // one that never ran, and the host already bounds how long this can take.
            await dispatcher.DispatchAsync([domainEvent], CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // One event failing is not a reason to stop reading the queue.
            logger.LogError(exception, "Handling {DomainEvent} failed.", domainEvent.GetType().Name);
        }
    }
}
