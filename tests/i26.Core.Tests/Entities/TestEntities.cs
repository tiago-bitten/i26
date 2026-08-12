using i26.Core.DomainEvents;
using i26.Core.Entities;
using i26.Core.Ids;
using i26.Core.Results;

namespace i26.Core.Tests.Entities;

// Declared here rather than inside the test classes: a typed id nested in another type is
// I26ID004, because the generator would have nowhere to put the members.
[TypedId("crs")]
internal readonly partial record struct CourseId;

[TypedId("std")]
internal readonly partial record struct StudentId;

/// <summary>Minted elsewhere, so this service is not allowed to invent one.</summary>
[TypedId("usr", Minted = false)]
internal readonly partial record struct UserId;

[TypedId("inv")]
internal readonly partial record struct InvoiceId;

[TypedId("ord")]
internal readonly partial record struct OrderId;

internal sealed record CoursePublished(CourseId Id) : IDomainEvent;

internal sealed class Course : Entity<CourseId>
{
    public Course()
    {
    }

    public Course(CourseId id) : base(id)
    {
    }

    public bool IsPublished { get; private set; }

    public void Publish()
    {
        IsPublished = true;
        Raise(new CoursePublished(Id));
    }
}

internal sealed class Student : Entity<StudentId>;

/// <summary>Another type carrying the same id type, which is still another entity.</summary>
internal sealed class OtherCourse(CourseId id) : Entity<CourseId>(id);

/// <summary>A local copy of something another service owns and identifies.</summary>
internal sealed class Mirror : Entity<UserId>
{
    public Mirror()
    {
    }

    public Mirror(UserId id) : base(id)
    {
    }
}

internal sealed class Invoice : DeletableEntity<InvoiceId>;

/// <summary>Refuses on its own terms before the base gets a say.</summary>
internal sealed class Order : DeletableEntity<OrderId>
{
    public static readonly Error Shipped = Error.Conflict("order.shipped");

    public bool HasShipped { get; set; }

    public override Result Delete() => HasShipped ? Shipped : base.Delete();
}
