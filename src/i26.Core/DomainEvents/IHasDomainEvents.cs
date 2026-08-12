namespace i26.Core.DomainEvents;

/// <summary>An entity that records the domain events it raised.</summary>
/// <remarks>
/// Two members and a list, implemented by the entity itself — there is no base entity here, and
/// nothing in this library decides what your entity looks like. Raising stays private: an event is
/// raised by the behaviour that caused it, not by whoever holds a reference to the entity.
/// </remarks>
public interface IHasDomainEvents
{
    /// <summary>What has been raised and not yet taken, in the order it was raised.</summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>Forgets the events raised so far.</summary>
    /// <remarks>Called by whoever took them for publication; an entity has no reason to call it.</remarks>
    void ClearDomainEvents();
}
