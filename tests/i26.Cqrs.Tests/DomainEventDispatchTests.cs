using System.Reflection;
using i26.Core.DomainEvents;
using Microsoft.Extensions.DependencyInjection;

namespace i26.Cqrs.Tests;

/// <summary>
/// Domain events go through the container the same way commands do, with one difference that is the
/// whole point of an event: as many handlers as there are, all of them run.
/// </summary>
public sealed class DomainEventDispatchTests
{
    private static readonly Assembly Handlers = typeof(AnnounceCourseHandler).Assembly;

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<CourseLog>();
        services.AddHandlers(Handlers);
        services.AddDomainEvents();

        return services.BuildServiceProvider();
    }

    private static Type[] ImplementationsOf(IServiceCollection services, Type serviceType) =>
        [.. services
            .Where(descriptor => descriptor.ServiceType == serviceType)
            .Select(descriptor => descriptor.ImplementationType!)];

    [Fact]
    public void Every_handler_of_an_event_is_registered()
    {
        var services = new ServiceCollection();

        services.AddHandlers(Handlers);

        // Ordered by name rather than by the order of the scan, which no rule here promises.
        Assert.Equal(
            [typeof(AnnounceCourseHandler), typeof(IndexCourseHandler)],
            ImplementationsOf(services, typeof(IDomainEventHandler<CoursePublishedDomainEvent>))
                .OrderBy(implementation => implementation.Name, StringComparer.Ordinal));
    }

    [Fact]
    public void Scanning_the_same_assembly_twice_registers_each_handler_once()
    {
        var services = new ServiceCollection();

        services.AddHandlers(Handlers);
        services.AddHandlers(Handlers);

        Assert.Equal(
            2,
            ImplementationsOf(services, typeof(IDomainEventHandler<CoursePublishedDomainEvent>)).Length);
    }

    [Fact]
    public void Handlers_are_registered_scoped()
    {
        var services = new ServiceCollection();

        services.AddHandlers(Handlers);

        Assert.All(
            services.Where(descriptor => descriptor.ServiceType.Namespace == typeof(IDomainEvent).Namespace),
            descriptor => Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime));
    }

    [Fact]
    public async Task Every_handler_runs()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

        await dispatcher.DispatchAsync([new CoursePublishedDomainEvent("Algebra")]);

        Assert.Equal(
            ["announce:Algebra", "index:Algebra"],
            scope.ServiceProvider.GetRequiredService<CourseLog>().Entries.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Handlers_resolve_in_the_scope_that_published()
    {
        using var provider = BuildProvider();

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        await first.ServiceProvider
            .GetRequiredService<IDomainEventDispatcher>()
            .DispatchAsync([new CoursePublishedDomainEvent("Algebra")]);

        Assert.NotEmpty(first.ServiceProvider.GetRequiredService<CourseLog>().Entries);
        Assert.Empty(second.ServiceProvider.GetRequiredService<CourseLog>().Entries);
    }

    [Fact]
    public async Task An_event_nobody_handles_is_not_an_error()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

        await dispatcher.DispatchAsync([new CourseArchivedDomainEvent()]);
    }

    [Fact]
    public async Task A_handler_that_throws_throws_what_it_threw()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

        // Not a TargetInvocationException: the dispatcher reaches the handler through a compiled
        // call rather than MethodInfo.Invoke, so the exception arrives as it was thrown.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync([new CourseRenamedDomainEvent("Algebra")]));

        Assert.Equal(RenameCourseHandler.Message, exception.Message);
    }

    [Fact]
    public async Task Publishing_the_queue_runs_the_handlers()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var queue = scope.ServiceProvider.GetRequiredService<DomainEventQueue>();
        queue.Enqueue([new CoursePublishedDomainEvent("Algebra")]);

        await queue.PublishAsync();

        Assert.Equal(
            ["announce:Algebra", "index:Algebra"],
            scope.ServiceProvider.GetRequiredService<CourseLog>().Entries.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_queue_is_one_per_scope()
    {
        using var provider = BuildProvider();

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var queue = first.ServiceProvider.GetRequiredService<DomainEventQueue>();

        Assert.Same(queue, first.ServiceProvider.GetRequiredService<DomainEventQueue>());
        Assert.NotSame(queue, second.ServiceProvider.GetRequiredService<DomainEventQueue>());
    }

    [Fact]
    public void A_dispatcher_of_your_own_is_kept()
    {
        var services = new ServiceCollection();

        services.AddScoped<IDomainEventDispatcher, ElsewhereDispatcher>();
        services.AddDomainEvents();

        Assert.Equal(
            [typeof(ElsewhereDispatcher)],
            ImplementationsOf(services, typeof(IDomainEventDispatcher)));
    }

    [Fact]
    public void It_refuses_a_null_argument()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddDomainEvents());
    }

    /// <summary>Stands in for a dispatcher that hands the events to a queue or an outbox.</summary>
    private sealed class ElsewhereDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(
            IReadOnlyList<IDomainEvent> domainEvents,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
