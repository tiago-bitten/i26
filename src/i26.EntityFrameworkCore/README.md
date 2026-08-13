# i26.EntityFrameworkCore

Everything the i26 primitives need from Entity Framework, so that the layer holding them does not
need Entity Framework.

```bash
dotnet add package i26.EntityFrameworkCore
```

- [Typed identifiers](#typed-identifiers)
- [Value objects](#value-objects)
- [Base entities](#base-entities)
- [Cursor pagination](#cursor-pagination)
- [Domain events](#domain-events)
- [Awaiting a query without an ORM](#awaiting-a-query-without-an-orm)

---

## Typed identifiers

One call in the `DbContext` maps every typed id in the assembly:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    => builder.ApplyTypedIdConventions(typeof(Course).Assembly);
```

Each one becomes a `text` column with collation `"C"`, storing the **full prefixed string**:

```sql
SELECT * FROM "Courses" WHERE "Id" = 'crs_01h455vb4pex5vsknk084sn02q';
```

Reading uses `Parse`, so a corrupted row — or one carrying another entity's prefix — fails loudly
instead of quietly becoming the wrong id.

`text` and `"C"` are Postgres' vocabulary, which is what the default assumes. Anywhere else, say so:

```csharp
builder.ApplyTypedIdConventions(TypedIdStorage.ProviderDefault, typeof(Course).Assembly);
```

That leaves the column type and the collation to the provider. Ordering then depends on that
collation being binary — without one, the database and `TypedId.Compare` can disagree about where a
page stops.

---

## Value objects

One call maps every i26 value object wherever it appears, so a property never says how to store
itself:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
{
    builder.ApplyTypedIdConventions(typeof(Course).Assembly);
    builder.ApplyValueObjectConventions();
}
```

An [`Email`](https://github.com/tiago-bitten/i26/blob/main/src/i26.Core/README.md#value-objects)
becomes a `varchar(254)` holding the address as text — lowercased, since that is how it was created,
which is what lets a unique index on the column mean what it looks like. Reading goes through
`Email.Parse`, so a row holding something else fails immediately rather than becoming an address
that never passed a check.

It comes with a comparer, and that part is not decoration: change tracking falls back to reference
equality for a class, so without one, assigning an address equal to the one already there would be
saved as an update saying nothing.

### From the configuration of the entity holding it

The convention maps the type; an index over one address is a decision about **that entity**, and
belongs where the entity is configured:

```csharp
internal sealed class UserConfiguration : EntityConfiguration<User, UserId>
{
    protected override void ConfigureEntity(EntityTypeBuilder<User> builder)
    {
        builder.HasEmail(user => user.Email, unique: true).IsRequired();
        builder.HasEmail(user => user.Recovery);          // optional, no index
    }
}
```

`HasEmail` applies the converter, the comparer and the width, so it stands on its own in a model
that never called `ApplyValueObjectConventions`. It answers with the property, so the configuration
goes on saying whatever else it has to say about it.

The unique index is worth a second's thought about what it means here: the address was lowercased
when it was created, so `TIAGO@example.com` and `tiago@example.com` collide on it — which is the
behaviour you want, and only true because the normalisation happened before the database saw it.

`EmailConverter` and `EmailComparer` are public too, for a property that would rather name them:

```csharp
builder.Property(contact => contact.Email)
    .HasConversion<EmailConverter, EmailComparer>()
    .HasMaxLength(Email.MaxLength);
```

---

## Base entities

An [`Entity<TId>`](https://github.com/tiago-bitten/i26/blob/main/src/i26.Core/README.md#base-entities)
declares `CreatedAt`, `UpdatedAt` and — when it is deletable — `DeletedAt`, and never sets any of
them: it has no clock, and one it was handed would be one more thing to pass around. This does it,
on the way into the save:

```csharp
using i26.EntityFrameworkCore.Entities;

builder.Services.AddDbContext<AppDbContext>((provider, options) => options
    .UseNpgsql(connectionString)
    .UseEntityTimestamps()          // or .UseEntityTimestamps(timeProvider) in a test
    .UseDomainEvents(provider));
```

An insert stamps `CreatedAt` and `UpdatedAt`; an update stamps `UpdatedAt`; and `DeletedAt` is
stamped when `IsDeleted` becomes true, because a soft delete reaches the database as an update like
any other. The time comes from a `TimeProvider`, so a test decides what "now" is instead of
asserting against the clock.

The properties keep their private setters — the interceptor writes through Entity Framework's own
metadata, which is what stops application code from choosing when something was created.

### The configuration, and what it does not need to say

```csharp
internal sealed class CourseConfiguration : EntityConfiguration<Course, CourseId>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Course> builder)
        => builder.Property(course => course.Title).HasMaxLength(200);
}
```

The base is four lines, and the reason is worth knowing: conventions already find the key, refuse to
generate the id, make the timestamps required and leave the domain events out of the model. Writing
`HasKey(e => e.Id)`, `ValueGeneratedNever()`, `IsRequired()` and `Ignore(e => e.DomainEvents)` is
writing what is already true — and every line of it is measured in `EntityConfigurationTests`, so a
version of Entity Framework that changes its mind fails there rather than in your application.

What is not free is the index a cursor page reads:

```sql
CREATE INDEX ... ON courses (created_at DESC, id DESC)
```

The base adds it. **The instant first and the id to break its ties** — the pair the other way round
is the one that is easy to write and the one a page cannot use. A table nobody pages says so, since
an index costs every write:

```csharp
protected override bool IsPaged => false;
```

A `DeletableEntity<TId>` uses the same base: hiding the deleted rows is a filter over the whole
model, not something a single configuration does.

### Names in the database

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // whatever else this configures…
    modelBuilder.ApplyLowercaseNames();
}
```

`UserAuth` becomes `userauth`, `CreatedAt` becomes `createdat`, and so do the keys, the indexes and
the foreign key constraints. On Postgres an identifier that is not lowercase has to be **quoted
everywhere, forever** — in every migration, every hand-written query and every psql session —
because an unquoted one is folded to lowercase and stops matching. Lowercasing once removes the
quotes from everything downstream.

Call it **last**: it rewrites the names decided before it, including the ones a configuration chose
by hand, which is what keeps a `ToTable("AuthProviders")` from being the one exception nobody
remembers.

### Hiding what was deleted

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Whatever else this configures, first…
    modelBuilder.ApplySoftDeleteFilter();
}
```

Every entity implementing `ISoftDeletable` gets `HasQueryFilter(row => !row.IsDeleted)`, built on
the concrete type rather than through the interface, since a member access through a cast has no
translation. Call it **after** the entity types exist: a filter applies to the model as it is at
that moment. A query that means to see them says `IgnoreQueryFilters()`.

---

## Cursor pagination

```csharp
using i26.EntityFrameworkCore.Pagination;

var page = await db.Courses
    .Where(course => course.TenantId == tenantId)
    .Select(course => new CourseRow
    {
        Id = course.Id,
        CreatedAt = course.CreatedAt,
        Title = course.Title,
    })
    .ToPagedResponseAsync<CourseRow, CourseId>(request, cancellationToken: ct);

return page.Value.Map(row => new CourseResponse(row.Id, row.Title));
```

The ordering is applied for you, so the query arrives filtered and nothing else. The result is a
`Result<PagedResponse<T>>`: a cursor that did not come from this API — truncated, hand-written, or
carrying another entity's id — is a validation failure, which reaches the caller as a 400 rather
than a 500.

Name both types when the id is a typed one; a row whose id is a `Guid` infers the rest and stays
`ToPagedResponseAsync(request)`.

The paging itself lives in `i26.Core`, over an
[`IAsyncQueryExecutor`](#awaiting-a-query-without-an-orm) — this overload is the one for code that
already has Entity Framework in front of it. An application layer that does not pages the same way,
passing the executor:

```csharp
using i26.Core.Pagination;

var page = await rows.ToPagedResponseAsync<CourseRow, CourseId>(executor, request, ct: ct);
```

> Project with an **object initializer**, not a constructor. Entity Framework binds
> `new CourseRow { CreatedAt = … }` back to the column it came from and can order by it; it cannot
> do the same for `new CourseRow(…)`. A response shape that takes a constructor is built afterwards,
> with `Map`.

---

## Domain events

An entity records what happened to it and something else reacts. Raising them is
[i26.Core](https://github.com/tiago-bitten/i26/blob/main/src/i26.Core/README.md#domain-events) and
running the handlers is
[i26.Cqrs](https://github.com/tiago-bitten/i26/blob/main/src/i26.Cqrs/README.md#domain-events); what
this package does is take them off the entities as they are saved.

### Collecting and publishing are two steps

Collecting takes the events off the entities as they are saved. Publishing hands them to their
handlers. They are separate because the moment to publish is not the moment to collect: an event
describes a row that a rollback would still take back, so it goes out after the transaction that
carries it has committed — and only whoever began that transaction knows when that is.

The `DomainEventQueue` is what sits between them, one per scope.

```csharp
using i26.Cqrs;
using i26.EntityFrameworkCore.DomainEvents;

builder.Services.AddHandlers(typeof(DependencyInjection).Assembly);  // every handler, events included
builder.Services.AddDomainEvents();                                  // the queue and a dispatcher

builder.Services.AddDbContext<AppDbContext>((provider, options) => options
    .UseNpgsql(connectionString)
    .UseDomainEvents(provider));
```

`UseDomainEvents` adds a `SaveChanges` interceptor that empties the entities into the queue on the
way into the save, and publishes on the way out:

| | What the interceptor does |
| --- | --- |
| `AfterSaveChanges` (default) | Collects, and publishes once the save has succeeded — unless a transaction is open on the context, in which case the events wait for whoever began it. |
| `Manual` | Collects. Publication is always an explicit `queue.PublishAsync(ct)`. |

Both modes collect *before* the save, not after: an entity being deleted is detached from the change
tracker the moment the save completes, and its event would go with it. A save that then fails
publishes nothing.

Two things worth knowing. The synchronous `SaveChanges` collects but never publishes — publication
is asynchronous — so a synchronous save leaves the events queued for the next publication. And a
handler that throws stops the ones behind it and surfaces out of whatever called `PublishAsync`,
which for `AfterSaveChanges` means out of `SaveChangesAsync`, after the data was written. If that
matters, publish somewhere you control, or dispatch to a background queue.

### Publishing where the transaction ends

A decorator that owns the transaction owns the publication. It asks for the same scoped queue the
interceptor filled:

```csharp
internal sealed class TransactionDecorator<TCommand>(
    ICommandHandler<TCommand> inner,
    AppDbContext db,
    DomainEventQueue events) : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    public async Task<Result> HandleAsync(TCommand command, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            return await inner.HandleAsync(command, ct);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var result = await inner.HandleAsync(command, ct);

        if (result.IsFailure)
        {
            await transaction.RollbackAsync(ct);
            events.Clear();

            return result;
        }

        await transaction.CommitAsync(ct);
        await events.PublishAsync(ct);

        return result;
    }
}
```

This composes with the default mode rather than replacing it: saves inside the transaction find one
open and stay quiet, and the commit is what publishes. `Manual` is for when you would rather the
interceptor never publish at all.

`PublishAsync` drains the queue before each dispatch and keeps going while handlers fill it again,
so a handler that saves further changes has its own events published by the same call — there is no
second publication to remember at the end of a handler.

---

## Awaiting a query without an ORM

```csharp
using i26.EntityFrameworkCore.Queries;

builder.Services.AddEfCoreAsyncQueries();
```

That registers the Entity Framework backend and the executor in front of it, both singleton. The
executor picks a backend **per query**, by looking at `IQueryable.Provider`, so a second store is a
second backend and nothing in the application layer changes:

```csharp
services.TryAddEnumerable(ServiceDescriptor.Singleton<IAsyncQueryBackend, MongoAsyncQueryBackend>());
```

With no backend able to run a query, the operator runs on the calling thread and the answer is still
right. That is what makes an application service testable against `List<T>.AsQueryable()` with no
database in sight — and it is also the trap to know about: a query that should have been
asynchronous and silently was not looks exactly like one that was.

---

## What it drags in

`Microsoft.EntityFrameworkCore.Relational`, and i26.Core. Pinned per target framework: 8.0.x on
net8.0, 9.0.x on net9.0, 10.0.x on net10.0.

---

Part of [i26](https://github.com/tiago-bitten/i26#readme).
