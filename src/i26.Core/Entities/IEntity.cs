using i26.Core.DomainEvents;

namespace i26.Core.Entities;

/// <summary>What every entity exposes, whatever its id is typed as.</summary>
/// <remarks>
/// The non-generic handle: infrastructure stamps the timestamps and drains the events without
/// knowing which id type an entity carries.
/// </remarks>
public interface IEntity : IHasDomainEvents
{
    /// <summary>When the row was first written.</summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>When the row was last written.</summary>
    DateTimeOffset UpdatedAt { get; }
}
