namespace i26.Core.DomainEvents;

/// <summary>Something that happened in the domain, raised by the entity it happened to.</summary>
/// <remarks>
/// The interface carries nothing on purpose: an event is a record of yours, named in the past tense,
/// holding what a handler needs and no more.
/// </remarks>
public interface IDomainEvent;
