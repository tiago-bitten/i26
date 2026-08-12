using i26.Core.DomainEvents;
using i26.Core.Ids;
using i26.Core.Pagination;

namespace i26.Core.Entities;

/// <summary>An entity identified by its own typed id.</summary>
/// <typeparam name="TId">The id type declared for this entity.</typeparam>
/// <remarks>
/// The id type is the type parameter, so `Course : Entity&lt;CourseId&gt;` gets a `CourseId` and
/// nothing else — passing a `StudentId` where the course is expected does not compile. Being
/// <see cref="ICursorPageable{TId}"/> comes free with the pair of members it already has, so any
/// entity can be paged by cursor without declaring anything.
/// </remarks>
public abstract class Entity<TId> : IEntity, ICursorPageable<TId>
    where TId : struct, ITypedId<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Creates an entity with a new id.</summary>
    /// <remarks>
    /// Also what a persistence layer calls when it materialises a row, which then overwrites the id
    /// it just minted. An id belonging to another service — <c>Minted = false</c> — is left unset
    /// instead, since only that service is allowed to invent one.
    /// </remarks>
    protected Entity() => Id = TId.Minted ? TypedId.New<TId>() : default;

    /// <summary>Creates an entity with the given id.</summary>
    protected Entity(TId id) => Id = id;

    /// <inheritdoc cref="ICursorPageable{TId}.Id" />
    public TId Id { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    /// <inheritdoc />
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>Records that something happened to this entity.</summary>
    /// <remarks>Protected: an event is raised by the behaviour that caused it, and by nothing else.</remarks>
    protected void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        _domainEvents.Add(domainEvent);
    }

    /// <summary>Whether this is the same entity — same type, same id.</summary>
    /// <remarks>
    /// Two entities of the same type with the same id are the same entity, however many times it
    /// was loaded. One whose id was never set is only equal to itself, because an id nobody
    /// assigned identifies nothing.
    /// </remarks>
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is Entity<TId> other
            && other.GetType() == GetType()
            && !Id.Equals(default(TId))
            && Id.Equals(other.Id);
    }

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
