using i26.Core.Results;
using Microsoft.Extensions.DependencyInjection;

namespace i26.Cqrs.Tests;

public class CqrsServiceCollectionExtensionsTests
{
    private static readonly System.Reflection.Assembly Handlers = typeof(PublishCourseHandler).Assembly;

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<CourseTitles>();
        services.AddHandlers(Handlers);

        return services.BuildServiceProvider();
    }

    private static Type[] ImplementationsOf(IServiceCollection services, Type serviceType) =>
        [.. services
            .Where(descriptor => descriptor.ServiceType == serviceType)
            .Select(descriptor => descriptor.ImplementationType!)];

    [Fact]
    public void It_registers_a_handler_for_each_shape_of_request()
    {
        var services = new ServiceCollection();

        services.AddHandlers(Handlers);

        Assert.Equal(
            [typeof(PublishCourseHandler)],
            ImplementationsOf(services, typeof(ICommandHandler<PublishCourseCommand>)));

        Assert.Equal(
            [typeof(CreateCourseHandler)],
            ImplementationsOf(services, typeof(ICommandHandler<CreateCourseCommand, Guid>)));

        Assert.Equal(
            [typeof(GetCourseHandler)],
            ImplementationsOf(services, typeof(IQueryHandler<GetCourseQuery, string>)));
    }

    [Fact]
    public void It_registers_them_scoped()
    {
        var services = new ServiceCollection();

        services.AddHandlers(Handlers);

        Assert.All(
            services.Where(descriptor => descriptor.ServiceType.Namespace == typeof(ICommand).Namespace),
            descriptor => Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime));
    }

    [Fact]
    public void A_handler_is_one_instance_per_scope()
    {
        using var provider = BuildProvider();

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var one = first.ServiceProvider.GetRequiredService<ICommandHandler<PublishCourseCommand>>();

        Assert.Same(one, first.ServiceProvider.GetRequiredService<ICommandHandler<PublishCourseCommand>>());
        Assert.NotSame(one, second.ServiceProvider.GetRequiredService<ICommandHandler<PublishCourseCommand>>());
    }

    [Fact]
    public async Task A_command_handler_resolves_with_its_dependencies_and_runs()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<PublishCourseCommand>>();

        var result = await handler.HandleAsync(new PublishCourseCommand("Algebra"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Algebra", provider.GetRequiredService<CourseTitles>().Last);
    }

    [Fact]
    public async Task A_command_handler_with_a_response_answers_with_the_value()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateCourseCommand, Guid>>();

        var result = await handler.HandleAsync(new CreateCourseCommand("Algebra"));

        Assert.Equal(CreateCourseHandler.Created, result.Value);
    }

    [Fact]
    public async Task A_query_handler_answers_the_query()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<IQueryHandler<GetCourseQuery, string>>();

        var result = await handler.HandleAsync(new GetCourseQuery(Guid.Empty));

        Assert.Equal("Algebra", result.Value);
    }

    [Fact]
    public async Task A_failure_travels_back_as_the_error_it_was()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<ArchiveCourseCommand>>();

        var result = await handler.HandleAsync(new ArchiveCourseCommand());

        Assert.True(result.IsFailure);
        Assert.Equal(ArchiveCourseHandler.NotFound, result.Error);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public void It_skips_what_cannot_be_instantiated()
    {
        var services = new ServiceCollection();

        services.AddHandlers(Handlers);

        var registered = services.Select(descriptor => descriptor.ImplementationType).ToArray();

        Assert.DoesNotContain(typeof(AbstractHandler), registered);
        Assert.DoesNotContain(typeof(OpenGenericHandler<>), registered);
    }

    [Fact]
    public void Scanning_the_same_assembly_twice_registers_each_handler_once()
    {
        var services = new ServiceCollection();

        services.AddHandlers(Handlers);
        services.AddHandlers(Handlers);

        Assert.Single(ImplementationsOf(services, typeof(ICommandHandler<PublishCourseCommand>)));
    }

    [Fact]
    public void It_takes_more_than_one_assembly()
    {
        var services = new ServiceCollection();

        services.AddHandlers(Handlers, typeof(ICommand).Assembly);

        Assert.Single(ImplementationsOf(services, typeof(ICommandHandler<PublishCourseCommand>)));
    }

    [Fact]
    public void Two_handlers_for_one_request_are_refused_and_both_are_named()
    {
        var services = new ServiceCollection();

        // A closed construction of an open generic: a real implementation type that the assembly
        // scan does not reach on its own, so the conflict is exactly the one under test.
        services.AddScoped(typeof(IQueryHandler<GetCourseQuery, string>), typeof(OpenGenericHandler<string>));

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddHandlers(Handlers));

        Assert.Contains(nameof(OpenGenericHandler<string>), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(GetCourseHandler), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(GetCourseQuery), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_request_already_taken_by_a_factory_is_refused_too()
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<PublishCourseCommand>>(_ => new PublishCourseHandler(new CourseTitles()));

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddHandlers(Handlers));

        Assert.Contains(nameof(PublishCourseHandler), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(PublishCourseCommand), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void It_refuses_a_null_argument()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddHandlers(Handlers));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddHandlers(null!));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddHandlers([null!]));
    }

    [Fact]
    public void An_unhandled_request_is_simply_not_registered()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        Assert.Null(scope.ServiceProvider.GetService<ICommandHandler<UnhandledCommand>>());
    }

    public sealed record UnhandledCommand : ICommand;
}
