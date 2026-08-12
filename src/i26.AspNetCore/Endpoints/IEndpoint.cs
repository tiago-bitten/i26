using Microsoft.AspNetCore.Routing;

namespace i26.AspNetCore.Endpoints;

/// <summary>One endpoint, declaring its own route next to its handler.</summary>
/// <remarks>
/// Found by <see cref="EndpointExtensions.AddEndpoints"/> and mapped by
/// <see cref="EndpointExtensions.MapEndpoints"/>. Implementations come out of the container, so they
/// can take constructor dependencies.
/// </remarks>
public interface IEndpoint
{
    /// <summary>Declares the route on the application, or on the group it was mapped into.</summary>
    void MapEndpoint(IEndpointRouteBuilder app);
}
