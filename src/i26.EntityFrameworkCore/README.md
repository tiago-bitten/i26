# i26.EntityFrameworkCore

Everything the i26 primitives need from Entity Framework, so that the layer holding them does not
need Entity Framework.

```bash
dotnet add package i26.EntityFrameworkCore
```

- [Typed identifiers](#typed-identifiers)
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
