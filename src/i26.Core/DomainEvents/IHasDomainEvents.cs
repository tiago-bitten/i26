namespace i26.Core.DomainEvents;

/// <summary>An entity that records the domain events it raised.</summary>
/// <remarks>Implemented by the entity itself: a list and these two members. Raising stays private.</remarks>
public interface IHasDomainEvents
{
    /// <summary>What has been raised and not yet taken, in order.</summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>Forgets the events raised so far.</summary>
    /// <remarks>Called by whoever took them for publication, not by the entity.</remarks>
    void ClearDomainEvents();
}
