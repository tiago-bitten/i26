using i26.Core.DomainEvents;
using i26.Cqrs;
using i26.Hosting.DomainEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace i26.Hosting.Tests;

/// <summary>
/// Against a host that really starts and stops, because the two things worth asserting — that the
/// handler runs somewhere else, and that stopping does not throw the queue away — are both about
/// the lifecycle rather than about the code.
/// </summary>
public sealed class BackgroundDomainEventTests
{
    private static IHost BuildHost(int concurrency = 1)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<Recorder>();
        builder.Services.AddScoped<ScopeMarker>();
        builder.Services.AddHandlers(typeof(RecordPublishedCourse).Assembly);
        builder.Services.AddBackgroundDomainEvents(options => options.Concurrency = concurrency);

        return builder.Build();
    }

    private static async Task PublishAsync(IHost host, params IDomainEvent[] domainEvents)
    {
        using var scope = host.Services.CreateScope();

        var queue = scope.ServiceProvider.GetRequiredService<DomainEventQueue>();
        queue.Enqueue(domainEvents);

        await queue.PublishAsync();
    }

    [Fact]
    public async Task Publishing_hands_the_event_over_and_returns()
    {
        using var host = BuildHost();
        await host.StartAsync();

        var recorder = host.Services.GetRequiredService<Recorder>();
        recorder.Hold(1);

        await PublishAsync(host, new CoursePublished("Algebra"));

        // Publishing came back while the handler is still in there, which is the whole point.
        await recorder.StartedAsync(1);
        Assert.Equal(["Algebra"], recorder.Handled);

        recorder.Release();
        await host.StopAsync();
    }

    [Fact]
    public async Task The_handler_runs_in_a_scope_of_its_own()
    {
        using var host = BuildHost();
        await host.StartAsync();

        using var publishing = host.Services.CreateScope();
        var publisher = publishing.ServiceProvider.GetRequiredService<ScopeMarker>();

        var queue = publishing.ServiceProvider.GetRequiredService<DomainEventQueue>();
        queue.Enqueue([new CoursePublished("Algebra")]);
        await queue.PublishAsync();

        var recorder = host.Services.GetRequiredService<Recorder>();
        await recorder.StartedAsync(1);

        Assert.True(recorder.Scopes.TryDequeue(out var handler));
        Assert.NotEqual(publisher.Id, handler);

        await host.StopAsync();
    }

    [Fact]
    public async Task An_event_whose_handler_throws_does_not_stop_the_next_one()
    {
        using var host = BuildHost();
        await host.StartAsync();

        await PublishAsync(host, new CourseArchived(), new CoursePublished("Algebra"));

        var recorder = host.Services.GetRequiredService<Recorder>();
        await recorder.StartedAsync(1);

        Assert.Equal(["Algebra"], recorder.Handled);

        await host.StopAsync();
    }

    [Fact]
    public async Task Stopping_the_host_still_hands_over_what_is_queued()
    {
        using var host = BuildHost();
        await host.StartAsync();

        var recorder = host.Services.GetRequiredService<Recorder>();
        recorder.Hold(1);

        await PublishAsync(
            host,
            new CoursePublished("first"),
            new CoursePublished("second"),
            new CoursePublished("third"));

        // Stop with two events still in the queue and one handler holding the door.
        await recorder.StartedAsync(1);
        var stopping = host.StopAsync();
        recorder.Release();

        await stopping;

        Assert.Equal(["first", "second", "third"], recorder.Handled);
    }

    [Fact]
    public async Task Publishing_after_the_host_stopped_is_dropped_rather_than_thrown()
    {
        using var host = BuildHost();
        await host.StartAsync();
        await host.StopAsync();

        await PublishAsync(host, new CoursePublished("Algebra"));

        Assert.Empty(host.Services.GetRequiredService<Recorder>().Handled);
    }

    [Fact]
    public async Task Events_are_handled_one_at_a_time_by_default()
    {
        using var host = BuildHost();
        await host.StartAsync();

        var recorder = host.Services.GetRequiredService<Recorder>();
        recorder.Hold(2);

        await PublishAsync(host, new CoursePublished("first"), new CoursePublished("second"));
        await recorder.StartedAsync(1);

        // The second one cannot have started: there is one reader and the first is holding it.
        Assert.Equal(["first"], recorder.Handled);

        recorder.Release();
        await recorder.StartedAsync(1);
        await host.StopAsync();
    }

    [Fact]
    public async Task Concurrency_lets_more_than_one_run_at_a_time()
    {
        using var host = BuildHost(concurrency: 2);
        await host.StartAsync();

        var recorder = host.Services.GetRequiredService<Recorder>();
        recorder.Hold(2);

        await PublishAsync(host, new CoursePublished("first"), new CoursePublished("second"));

        // Both started while both are holding, which one reader could not have done.
        await recorder.StartedAsync(2);
        Assert.Equal(2, recorder.Handled.Count);

        recorder.Release();
        await host.StopAsync();
    }

    [Fact]
    public void The_registration_takes_over_the_dispatcher_in_either_order()
    {
        var backgroundFirst = new ServiceCollection();
        backgroundFirst.AddBackgroundDomainEvents();
        backgroundFirst.AddDomainEvents();

        var backgroundLast = new ServiceCollection();
        backgroundLast.AddDomainEvents();
        backgroundLast.AddBackgroundDomainEvents();

        // The in-process dispatcher is scoped and this one is not, which is the difference between
        // handling the event here and queueing it for someone else.
        Assert.All(
            [backgroundFirst, backgroundLast],
            services => Assert.Equal(
                ServiceLifetime.Singleton,
                Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IDomainEventDispatcher))
                    .Lifetime));
    }

    [Fact]
    public void The_runner_the_background_service_needs_is_registered_either_way()
    {
        var services = new ServiceCollection();

        services.AddBackgroundDomainEvents();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(InProcessDomainEventDispatcher));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(DomainEventQueue));
    }

    [Fact]
    public void Registering_twice_adds_one_of_everything()
    {
        var services = new ServiceCollection();

        services.AddBackgroundDomainEvents();
        services.AddBackgroundDomainEvents();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IDomainEventDispatcher));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void A_size_that_makes_no_sense_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ServiceCollection().AddBackgroundDomainEvents(options => options.Capacity = 0));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ServiceCollection().AddBackgroundDomainEvents(options => options.Concurrency = 0));

        Assert.Throws<ArgumentNullException>(
            () => ((IServiceCollection)null!).AddBackgroundDomainEvents());
    }
}
