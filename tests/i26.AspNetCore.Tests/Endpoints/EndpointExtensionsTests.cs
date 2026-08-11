using i26.AspNetCore.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace i26.AspNetCore.Tests.Endpoints;

public class EndpointExtensionsTests
{
    private static Type[] RegisteredEndpoints(IServiceCollection services) =>
        [.. services
            .Where(descriptor => descriptor.ServiceType == typeof(IEndpoint))
            .Select(descriptor => descriptor.ImplementationType!)];

    private static WebApplication CreateApp(bool registerEndpoints = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<EndpointDependency>();

        if (registerEndpoints)
        {
            builder.Services.AddEndpoints(typeof(EndpointExtensionsTests).Assembly);
        }

        return builder.Build();
    }

    private static string[] MappedRoutes(WebApplication app) =>
        [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText!)];

    [Fact]
    public void AddEndpoints_registers_every_concrete_endpoint_of_the_assembly()
    {
        var services = new ServiceCollection();

        services.AddEndpoints(typeof(EndpointExtensionsTests).Assembly);

        var registered = RegisteredEndpoints(services);

        Assert.Contains(typeof(GetCourseEndpoint), registered);
        Assert.Contains(typeof(CreateCourseEndpoint), registered);
        Assert.Contains(typeof(DependentEndpoint), registered);
    }

    [Fact]
    public void AddEndpoints_registers_them_as_transient()
    {
        var services = new ServiceCollection();

        services.AddEndpoints(typeof(EndpointExtensionsTests).Assembly);

        Assert.All(
            services.Where(descriptor => descriptor.ServiceType == typeof(IEndpoint)),
            descriptor => Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime));
    }

    [Fact]
    public void AddEndpoints_skips_what_cannot_be_instantiated()
    {
        var services = new ServiceCollection();

        services.AddEndpoints(typeof(EndpointExtensionsTests).Assembly);

        var registered = RegisteredEndpoints(services);

        Assert.DoesNotContain(typeof(AbstractEndpoint), registered);
        Assert.DoesNotContain(typeof(OpenGenericEndpoint<>), registered);
        Assert.DoesNotContain(typeof(IEndpoint), registered);
    }

    [Fact]
    public void AddEndpoints_called_twice_registers_each_endpoint_once()
    {
        var services = new ServiceCollection();
        var assembly = typeof(EndpointExtensionsTests).Assembly;

        services.AddEndpoints(assembly);
        services.AddEndpoints(assembly);

        Assert.Single(RegisteredEndpoints(services), type => type == typeof(GetCourseEndpoint));
    }

    [Fact]
    public void AddEndpoints_takes_more_than_one_assembly()
    {
        var services = new ServiceCollection();

        services.AddEndpoints(typeof(EndpointExtensionsTests).Assembly, typeof(IEndpoint).Assembly);

        Assert.Contains(typeof(GetCourseEndpoint), RegisteredEndpoints(services));
    }

    [Fact]
    public async Task MapEndpoints_maps_the_route_each_endpoint_declares()
    {
        await using var app = CreateApp();

        app.MapEndpoints();

        var routes = MappedRoutes(app);

        Assert.Contains(GetCourseEndpoint.Route, routes);
        Assert.Contains(CreateCourseEndpoint.Route, routes);
    }

    [Fact]
    public async Task MapEndpoints_maps_under_the_prefix_of_the_group_it_is_called_on()
    {
        await using var app = CreateApp();

        var group = app.MapGroup("v1");
        group.MapEndpoints();

        var routes = MappedRoutes(app);

        Assert.Contains($"v1/{GetCourseEndpoint.Route}", routes);
        Assert.DoesNotContain(GetCourseEndpoint.Route, routes);
    }

    [Fact]
    public async Task MapEndpoints_resolves_the_dependencies_of_an_endpoint()
    {
        await using var app = CreateApp();

        app.MapEndpoints();

        Assert.Contains(DependentEndpoint.Route, MappedRoutes(app));
    }

    [Fact]
    public async Task MapEndpoints_returns_the_builder_it_was_given()
    {
        await using var app = CreateApp();

        var group = app.MapGroup("v1");

        Assert.Same(group, group.MapEndpoints());
    }

    [Fact]
    public async Task MapEndpoints_without_AddEndpoints_says_so_instead_of_mapping_nothing()
    {
        await using var app = CreateApp(registerEndpoints: false);

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapEndpoints());

        Assert.Contains(nameof(EndpointExtensions.AddEndpoints), exception.Message, StringComparison.Ordinal);
        Assert.Empty(MappedRoutes(app));
    }
}
