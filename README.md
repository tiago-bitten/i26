# i26

[![ci](https://github.com/tiago-bitten/i26/actions/workflows/ci.yml/badge.svg)](https://github.com/tiago-bitten/i26/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4.svg)](#compatibility)

Building blocks for .NET services: **strongly typed identifiers** that read the same in the API, the
logs and the database, and a **result pattern** that carries business failures without exceptions —
all the way to an RFC 9457 problem response.

```csharp
// An id that cannot be mistaken for another entity's
var id = CourseId.New();          // crs_01h455vb4pex5vsknk084sn02q

// A failure that travels as a value, not as a throw
public async Task<Result<Course>> HandleAsync(PublishCourseCommand command, CancellationToken ct)
{
    var course = await courses.FindAsync(command.Id, ct);

    if (course is null)
    {
        return CourseErrors.NotFound;      // implicit conversion, no ceremony
    }

    return course.Publish();               // Result<Course>
}

// An endpoint that says what it can answer, and answers it
app.MapPost("courses/{id}/publish", Handle)
    .ProducesProblem(CourseErrors.NotFound, CourseErrors.AlreadyPublished);
```

---

## Contents

- [Packages](#packages)
- [Installing](#installing)
- [Typed identifiers](#typed-identifiers)
- [Result pattern](#result-pattern)
- [Commands and queries](#commands-and-queries)
- [Domain events](#domain-events)
- [Awaiting a query without an ORM](#awaiting-a-query-without-an-orm)
- [Specifications](#specifications)
- [Cursor pagination](#cursor-pagination)
- [ASP.NET Core](#aspnet-core)
- [Putting it together](#putting-it-together)
- [Error types](#error-types)
- [Compatibility](#compatibility)
- [Building](#building)

---

## Packages

| Package | What it holds | Depends on |
| --- | --- | --- |
| `i26.Core` | Typed ids and their generator, `Result`/`Error`, cursor paging, domain event and query contracts | nothing outside the BCL |
| `i26.Cqrs` | Command, query and domain event contracts, the handler registration, an in-process dispatcher | `Microsoft.Extensions.DependencyInjection.Abstractions` |
| `i26.EntityFrameworkCore` | Typed id conventions, cursor paging over `IQueryable`, domain event collection on save | `Microsoft.EntityFrameworkCore.Relational` |
| `i26.Dapper` | Typed id handlers, cursor paging over a hand-written query | `Dapper` |
| `i26.AspNetCore` | Problem responses, endpoint discovery, global exception handler | ASP.NET Core shared framework |

`i26.Core` has **no external dependencies** by design — it is meant to sit in a domain project
without dragging a web stack or an ORM behind it.

## Installing

The packages are not on nuget.org yet. While the first release is being cut, reference the projects
directly:

```bash
dotnet add reference ../i26/src/i26.Core/i26.Core.csproj
```

Once published:

```bash
dotnet add package i26.Core
dotnet add package i26.Cqrs
dotnet add package i26.EntityFrameworkCore
dotnet add package i26.Dapper
dotnet add package i26.AspNetCore
```

---

## Typed identifiers

Ids follow the [TypeID](https://github.com/jetify-com/typeid) format: a prefix that names the
entity, an underscore, and a UUIDv7 encoded in lowercase Crockford base32.

```
crs_01h455vb4pex5vsknk084sn02q
└┬┘ └────────────┬───────────┘
 │               │
 │               └─ 16 bytes of UUIDv7 in 26 characters: 48 bits of timestamp
 │                  first, so the text sorts by creation
 │
 └─ the entity, in up to three letters, fixed in the type
```

### Declaring one

```csharp
using i26.Core.Ids;

[TypedId("crs")]
public readonly partial record struct CourseId;
```

That is the whole declaration. A generator ships inside `i26.Core` and writes the rest — the
interface, `Value`, `New`, `ToString`, `Parse`, `TryParse`, `CompareTo` and the comparison
operators — so the lines that never vary between one id and the next are not yours to keep in sync.
A prefix past three characters says so on the attribute:
`[TypedId("workspace", UsesExtendedPrefix = true)]`.

A `record struct` is handed equality by the compiler. Declare a plain `struct` instead and the
generator writes `Equals`, `GetHashCode`, `==` and `!=` as well, so the two shapes behave the same
and neither one falls back to `ValueType.Equals`.

Because the generator sees every id in the compilation, the rules become compile errors instead of
runtime ones:

| | |
| --- | --- |
| `I26ID001` | the type is not `partial` |
| `I26ID002` | the prefix is empty, uppercase, or longer than the rule allows |
| `I26ID003` | two ids declare the same prefix — the one mistake no per-type check can catch |
| `I26ID004` | the type is nested inside another |
| `I26ID005` | the type is generic, `file`-local or a `ref struct`, so the members would land elsewhere |
| `I26ID006` | the type declares a primary constructor, which the generator already writes |

<details>
<summary>The same id written by hand</summary>

Nothing depends on the generator: what it writes is exactly this, and the two are interchangeable.

```csharp
public readonly record struct CourseId(Guid Value) : ITypedId<CourseId>
{
    public static string Prefix => "crs";

    public static CourseId FromGuid(Guid value) => new(value);
    public static CourseId New() => TypedId.New<CourseId>();

    public override string ToString() => TypedId.Format(this);

    public static CourseId Parse(string s, IFormatProvider? _ = null) => TypedId.Parse<CourseId>(s);

    public static bool TryParse(string? s, IFormatProvider? _, out CourseId result)
        => TypedId.TryParse(s, out result);

    public int CompareTo(CourseId other) => TypedId.Compare(this, other);
}
```

`CompareTo` is on the interface rather than left to each id, because everything that orders ids —
`Order()`, a `SortedSet`, the keyset predicate behind cursor pagination — reaches for
`IComparable<T>` and finds nothing otherwise. The comparison operators are not on the interface, so
a hand-written id that wants `left < right` writes them; the generator always does.

</details>

`CourseId` and `StudentId` are different types. Passing one where the other is expected does not
compile — which is the whole point.

### The prefix rule

**Up to three lowercase letters.** The prefix shows up in every id, every log line and every URL,
and three characters are enough to tell entities apart at a glance — `usr`, `ord`, `crs`, `inv`.

It is checked, once per id type, the first time one is formatted or parsed. An empty prefix, an
uppercase letter, a digit, an underscore or a fourth character stops the type with a message saying
which rule it broke.

There is one mistake no per-type check can catch: **two entities picking the same prefix.** Nothing
stops `CourseId` and `ClassroomId` from both declaring `crs`, the code goes on compiling, and
`crs_01h455…` quietly stops saying which entity it belongs to. Ids declared with `[TypedId]` are
checked against each other while the project compiles; for ids written by hand, one test in the
project that declares them settles it:

```csharp
[Fact]
public void Typed_id_prefixes_are_valid_and_unique()
    => TypedIdPrefix.ValidateAll(typeof(CourseId).Assembly);
```

That sweeps every typed id in the assembly — non-public ones included — checking each prefix against
the rules and refusing any that repeats, naming both types. `TypedIdPrefix.Validate<CourseId>()`
checks a single id, and `ValidateAll` also takes an explicit list of types when you want to scope it.

When three really are not enough, say so next to the prefix:

```csharp
public static string Prefix => "workspace";
public static bool UsesExtendedPrefix => true;   // up to ten
```

The flag defaults to `false`, so a long prefix is always a decision someone wrote down rather than
one that crept in. Ten is the ceiling either way.

### Using it

```csharp
var id = CourseId.New();

id.ToString();                     // "crs_01h455vb4pex5vsknk084sn02q"
CourseId.Parse(id.ToString());     // back to the id
TypedId.Compare(first, second);    // order, oldest first
TypedId.Empty<CourseId>();         // the zero id, which is also default(CourseId)

CourseId.TryParse("std_01h455vb4pex5vsknk084sn02q", null, out _);   // false: wrong prefix
```

Parsing is strict: exact length, exact prefix, lowercase only, and no `i`, `l`, `o` or `u` — every
id has exactly one textual form.

**Reading the instant an id was minted at is a `Try`.** Parsing checks the prefix and the alphabet,
never the 128 bits behind them, so an id that arrived from a route can be well formed and still
carry bits that name no instant at all:

```csharp
TypedId.TryGetTimestamp(id, out var createdAt);   // false for anything but a UUIDv7 in range
TypedId.GetTimestamp(id);                         // throws ArgumentException instead
```

Use the `Try` form for an id you were handed and the plain one for an id you minted. `GetTimestamp`
checks the version nibble, so a UUIDv4 is refused rather than read as an instant eight millennia
from now.

### Ordering

The encoding preserves order, so ids sort by creation time — as ids, as strings, and in the
database:

```csharp
ids.Order();                                                     // IComparable<CourseId>
ids.Select(id => id.ToString()).Order(StringComparer.Ordinal);   // the same order
first < second;                                                  // and the same again
```

All three compare the bytes big-endian, which is the order a `text COLLATE "C"` column is in.
Reaching for `Guid.CompareTo` instead does **not** give it: `Guid.ToByteArray()` is little-endian
for the first three fields.

One caveat the encoding cannot fix: two ids minted in the same millisecond are ordered by their
random bits, not by which came first. The order is stable and exact — which is all a page boundary
needs — but within a millisecond it is not chronological.

### JSON

One registration covers every typed id in the process, present and future:

```csharp
options.Converters.Add(new TypedIdJsonConverterFactory());
```

Ids serialize as the prefixed string, and work as dictionary keys. An invalid value throws
`JsonException` instead of silently landing as `default`.

### Route and query binding

`ITypedId<TSelf>` derives from `IParsable<TSelf>`, which is all minimal APIs need:

```csharp
app.MapGet("courses/{id}", (CourseId id) => /* ... */);
```

A malformed id in the route comes back as a 400 through the
[exception handler](#global-exception-handler), naming the offending field.

### Entity Framework Core

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

### The decisions behind it

| Decision | Why |
| --- | --- |
| Ids are created by the application, never by the database | The entity is whole before it is saved, and nothing needs a round trip to learn its own id |
| UUIDv7 underneath | 48 bits of timestamp up front means new rows land at the right end of the B-tree, unlike a random UUIDv4 |
| Stored as `text`, prefix included | The id in a log line, in a URL and in a `WHERE` clause is one copy-paste. The extra bytes buy ergonomics, and that trade was made on purpose |
| Collation `"C"` | Sorts byte by byte, so database order equals creation order and the index does not depend on the server locale |
| One value object per entity | The compiler catches a `StudentId` passed where a `CourseId` belongs |

### Referencing another service's ids

Say that this service does not mint the prefix, and the generator leaves `New()` off:

```csharp
[TypedId("usr", Minted = false)]   // the prefix the auth service mints, not ours
public readonly partial record struct AuthUserId;
```

It is a typed reference, not a foreign key: it parses, formats, compares and persists like any other
id, and the only thing missing is the one operation that would be a lie.

`Minted = false` is enforced rather than implied. Leaving `New()` off a hand-written id was the
whole of the old convention, and `TypedId.New<AuthUserId>()` walked straight around it — now it
throws, naming the prefix and the service that owns it.

---

## Result pattern

Business rules return a `Result`. Exceptions stay for what is genuinely exceptional — a broken
dependency, a bug — so the expected failure paths stay visible in the signature.

### Errors are codes

An `Error` is a stable code and a kind of failure. It carries **no message**: the text depends on
the caller's language, which is a boundary concern.

```csharp
using i26.Core.Results;

public static class CourseErrors
{
    public static readonly Error NotFound = Error.NotFound("course.notFound");
    public static readonly Error AlreadyPublished = Error.Conflict("course.alreadyPublished");

    // An error whose message needs a value is a method, not a field
    public static Error TitleTooLong(int max) => Error.Validation("course.title.tooLong", max);
}
```

Codes are `dot.camelCase` — root naming the entity, inner segments in camel case:
`course.notFound`, `classroom.teachingLevel.required`. They belong in a `{Entity}Errors` class next
to the entity, never inline at the call site: the code is the contract the client keys off.

**Identity is the code and the type, nothing else.** Arguments and metadata are payload, so an error
still matches the canonical one it came from:

```csharp
result.Error == CourseErrors.TitleTooLong(500);   // true, whatever the max was
```

### Returning one

Implicit conversions keep the noise down — no `Result.Failure(...)` when returning the error itself
says it:

```csharp
public Result<Course> Publish()
{
    if (IsPublished)
    {
        return CourseErrors.AlreadyPublished;   // Error   → failed Result<Course>
    }

    IsPublished = true;

    return this;                                // Course  → successful Result<Course>
}
```

Reading `Value` on a failure throws, so check first and give it a name:

```csharp
var courseResult = Course.Create(title);

if (courseResult.IsFailure)
{
    return courseResult.Error;
}

var course = courseResult.Value;
```

### Chaining

Every combinator short-circuits: once a result fails, the rest does not run and the original error
is carried through.

| | What it does |
| --- | --- |
| `Match(onSuccess, onFailure)` | Folds both branches into one value — usually the last line of an endpoint |
| `Map(value => …)` | Transforms the value |
| `Bind(value => …)` | Runs the next operation, which returns a result of its own |
| `Tap(value => …)` | Runs a side effect and passes the result along |
| `Ensure(predicate, error)` | Fails a success that does not hold a condition |

Each has an asynchronous counterpart — `MatchAsync`, `MapAsync`, `BindAsync`, `TapAsync`,
`EnsureAsync` — accepting a `Task<Result<T>>` on the left, an async continuation on the right, or
both:

```csharp
return await courses.FindAsync(id, ct)
    .EnsureAsync(course => !course.IsPublished, CourseErrors.AlreadyPublished)
    .BindAsync(course => publisher.PublishAsync(course, ct))
    .MatchAsync(Results.Ok, ProblemResults.Problem);
```

### Several failures at once

`ValidationError` collects them so the caller does not fix one field per round trip:

```csharp
Result<Course> result = ValidationError.FromResults([titleResult, priceResult, dateResult]);
```

### Localization

The text comes from an `IErrorTranslator`, resolved at the boundary and never written back into the
error:

```csharp
internal sealed class ResourceErrorTranslator(IStringLocalizer localizer) : IErrorTranslator
{
    public string? Describe(Error error)
    {
        var template = localizer[error.Code];

        if (template.ResourceNotFound)
        {
            return null;    // no entry: the response simply omits the detail
        }

        return error.Arguments is { Count: > 0 } arguments
            ? string.Format(CultureInfo.CurrentCulture, template.Value, [.. arguments])
            : template.Value;
    }
}
```

`Error.Arguments` fills the placeholders, so `course.title.tooLong` renders as *"Title is longer
than 200 characters"* in any language without the domain ever composing a sentence.

---

## Commands and queries

A request is a record, its handler is a class, and the handler answers with a `Result`. There is no
mediator in the middle: a caller asks the container for the handler of the exact request it means,
which the compiler checks and a reader can follow to its declaration.

```csharp
using i26.Cqrs;

public sealed record PublishCourseCommand(CourseId Id) : ICommand;              // no response
public sealed record CreateCourseCommand(string Title) : ICommand<CourseId>;    // with one
public sealed record GetCourseQuery(CourseId Id) : IQuery<CourseResponse>;      // reads only

internal sealed class PublishCourseHandler(ICourseRepository courses)
    : ICommandHandler<PublishCourseCommand>
{
    public async Task<Result> HandleAsync(PublishCourseCommand command, CancellationToken ct = default)
    {
        var course = await courses.FindAsync(command.Id, ct);

        if (course is null)
        {
            return CourseErrors.NotFound;
        }

        return course.Publish();
    }
}
```

The response type lives on the request, so `ICommandHandler<CreateCourseCommand, CourseId>` and
every call site agree on it without anyone restating it.

### Registration

The library cannot see your handlers, so you call this from wherever the application layer wires
itself up:

```csharp
using i26.Cqrs;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddHandlers(typeof(DependencyInjection).Assembly);
        // validators, decorators, services…

        return services;
    }
}
```

Every handler in the assembly is registered scoped, under the interfaces it implements — internal
and private ones included, since a handler is an implementation detail of the application layer and
has no reason to be public. Endpoints then ask for one directly:

```csharp
[FromServices] ICommandHandler<PublishCourseCommand> handler
```

**Two handlers for one request is refused**, naming both, instead of resolving to whichever was
scanned last. That is the failure mode of copying a handler and forgetting to change the request it
handles, and it is silent everywhere else. Scanning the same assembly twice is harmless.

### Decorators

Nothing here is decorated for you — validation, logging, transactions and caching are decisions
about your application, not about a library. The registration is the plain closed-generic kind, so
[Scrutor](https://github.com/khellang/Scrutor) wraps it the usual way:

```csharp
services.Decorate(typeof(ICommandHandler<,>), typeof(ValidationDecorator.CommandHandler<,>));
services.Decorate(typeof(ICommandHandler<>), typeof(LoggingDecorator.CommandBaseHandler<>));
```

---

## Domain events

An entity records what happened to it; something else reacts once the change is committed. There is
no base entity here to inherit from — an entity says it raises events by implementing
`IHasDomainEvents`, which is a list and two members.

```csharp
using i26.Core.DomainEvents;

public sealed record CoursePublishedDomainEvent(CourseId Id) : IDomainEvent;

public sealed class Course : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    public Result Publish()
    {
        if (IsPublished)
        {
            return CourseErrors.AlreadyPublished;
        }

        IsPublished = true;
        _domainEvents.Add(new CoursePublishedDomainEvent(Id));

        return Result.Ok();
    }
}
```

Raising is private on purpose: an event is added by the behaviour that caused it, never by whoever
happens to hold a reference to the entity. The interface exposes only what the infrastructure needs
— what was raised, and a way to forget it once taken.

Handlers are classes, as many per event as you like:

```csharp
internal sealed class NotifyStudentsHandler(IEmailer emailer)
    : IDomainEventHandler<CoursePublishedDomainEvent>
{
    public Task HandleAsync(CoursePublishedDomainEvent domainEvent, CancellationToken ct = default)
        => emailer.AnnounceAsync(domainEvent.Id, ct);
}
```

`AddHandlers` registers them alongside the command and query handlers. Two handlers for one command
is still refused; two handlers for one event is the point of an event, so both are kept.

Nothing has to be configured on the model. A get-only `IReadOnlyList<IDomainEvent>` is neither a
primitive collection nor a navigation candidate, so Entity Framework leaves it out of the model on
its own — there is a test that pins exactly that.

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

### A dispatcher of your own

`AddDomainEvents` registers one that runs the handlers in process, in the scope that published, and
registers it only if nothing else claimed `IDomainEventDispatcher`. Handing the events to a
background queue instead is a class:

```csharp
internal sealed class BackgroundDomainEventsDispatcher(IBackgroundJobScheduler scheduler)
    : IDomainEventDispatcher
{
    public Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            scheduler.Enqueue<ProcessDomainEventJob>(domainEvent, ct);
        }

        return Task.CompletedTask;
    }
}
```

```csharp
services.AddScoped<IDomainEventDispatcher, BackgroundDomainEventsDispatcher>();
services.AddDomainEvents();
```

---

## Awaiting a query without an ORM

An application layer can write LINQ against an `IQueryable<T>` with no reference to anything —
`Where`, `Select`, `OrderBy` and the rest are the BCL. What it cannot do is **await** one:
`ToListAsync` and `CountAsync` belong to Entity Framework, and one `using` for them drags the ORM
into the layer that was supposed to know nothing about it.

`IAsyncQueryExecutor` is that one seam, and it lives in `i26.Core`:

```csharp
using i26.Core.Queries;

internal sealed class ListCoursesHandler(ICourseQueries courses, IAsyncQueryExecutor executor)
    : IQueryHandler<ListCoursesQuery, PagedResponse<CourseRow>>
{
    public async Task<Result<PagedResponse<CourseRow>>> HandleAsync(
        ListCoursesQuery query, CancellationToken ct = default)
    {
        var rows = courses.Published()                              // IQueryable<Course>, from your port
            .Where(course => course.TenantId == query.TenantId)
            .Select(course => new CourseRow { Id = course.Id, CreatedAt = course.CreatedAt });

        return await rows.ToPagedResponseAsync<CourseRow, CourseId>(executor, query.Page, ct: ct);
    }
}
```

That project references `i26.Core` and `i26.Cqrs`. No `Microsoft.EntityFrameworkCore` anywhere.

### The two methods behind it

```csharp
Task<TResult> ExecuteAsync<T, TResult>(IQueryable<T> query, Expression<Func<IQueryable<T>, TResult>> terminal, CancellationToken ct = default);
Task<List<T>>  ToListAsync<T>(IQueryable<T> query, CancellationToken ct = default);
```

The first takes the terminal operator as an expression and hands it to the provider — which is what
`CountAsync` does inside Entity Framework, one operator at a time. Writing it once means the
familiar names are extension methods over it rather than interface members, and an operator nobody
wrote a method for is the operator itself:

```csharp
await executor.CountAsync(invoices, ct);
await executor.FirstOrDefaultAsync(invoices, invoice => invoice.Number == number, ct);
await executor.ExecuteAsync(invoices, q => q.Sum(invoice => invoice.Amount), ct);   // no SumAsync needed
await executor.ExecuteAsync(invoices, q => q.GroupBy(i => i.Status).Count(), ct);
```

`Count`, `LongCount`, `Any`, `All`, `First`, `FirstOrDefault`, `Single`, `SingleOrDefault`, `ToList`
and `ToArray` have methods, with and without a predicate. Everything else is one `ExecuteAsync`.

> A predicate always reaches the provider as a **quoted lambda**, never as a captured variable —
> `q => q.Count(predicate)` inside an expression tree becomes a field access of type
> `Expression<Func<T, bool>>`, which no provider can read. That is why the predicate overloads apply
> a `Where` and count what is left; same SQL, and it works on the in-memory fallback too.

### Wiring it

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

### Where the abstraction stops

`Include`, `AsNoTracking`, `AsSplitQuery` and `IgnoreQueryFilters` are Entity Framework, and no
interface here hides them. The test for whether something belongs in a port is whether the next
store could answer it: `AsNoTracking` only means something because Entity Framework has a change
tracker, and `Include` is a loading strategy that a document store either does not need or spells
`$lookup`. Neither survives the move, so neither is a port.

What survives is the intent. The port says which rows, the adapter decides how they are fetched:

```csharp
// Application declares the port
public interface ICourseQueries
{
    IQueryable<Course> Published();
}

// The adapter is where the ORM lives
internal sealed class CourseQueries(AppDbContext db) : ICourseQueries
{
    public IQueryable<Course> Published() =>
        db.Courses.AsNoTracking().Where(course => course.IsPublished);
}
```

A port shaped as `Get(bool asNoTracking, params string[] includes)` fails that test twice over: the
first parameter is Entity Framework's vocabulary in an application's interface, and the second is
`Include` by magic string, which no compiler checks and no rename follows.

`Include` is worth a second look even inside the adapter. On a read, what the caller wants is a
projection, and a projection is plain LINQ the application can write itself — the ORM turns
`course.Teacher.Name` into a join with no `Include` in sight:

```csharp
courses.Published().Select(course => new CourseRow
{
    Id = course.Id,
    CreatedAt = course.CreatedAt,
    Teacher = course.Teacher.Name,
});
```

Which leaves `Include` where it belongs: loading a tracked aggregate to change it. That path is not
an `IQueryable` at all — it is a repository handing back the entity.

---

## Specifications

A rule that has to be asked twice — of a row you are holding, and of a table you are querying — is
worth writing once:

```csharp
using i26.Core.Specifications;

public sealed class ConflictingPhone(UserId exclude, IReadOnlyCollection<string> digits)
    : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression() =>
        user => user.Id != exclude && user.Phones.Any(phone => digits.Contains(phone.Digits));
}
```

```csharp
if (new ConflictingPhone(command.UserId, digits).IsSatisfiedBy(user))   // in memory
await executor.AnyAsync(users.Where(new ConflictingPhone(command.UserId, digits)), ct);   // in SQL
```

`Where` takes the specification directly — no `spec.ToExpression()` at the call site — and the
compiled form is cached per instance, because compiling an expression costs a thousand times what
calling it does and asking one rule of every item of a list is how `IsSatisfiedBy` gets used.

### Composing

```csharp
var wanted = new Published().And(new Popular(minimum: 10).Or(new Featured())).Not();

var rows = await executor.ToListAsync(courses.Where(wanted), ct);
```

`And`, `Or` and `Not` are extension methods on `ISpecification<T>`, so a rule that implements the
interface without inheriting `Specification<T>` composes the same way, and what comes back is a
`Specification<T>` that caches like any other.

What comes out of a composition is `course => a && b` — one parameter, no `Invoke`. The shorter
implementation, `Expression.Invoke(left, p) && Expression.Invoke(right, p)`, translates fine under
Entity Framework, which removes invocations before it translates anything; it is the second provider
behind an `IAsyncQueryBackend` that would not. Rebinding the parameter asks nobody for the favour,
and a test pins that the tree has no invocation left in it.

### Filters that may not apply

A search request with five optional fields is five `if`s around a reassignment, or this:

```csharp
using i26.Core.Queries;

var rows = courses
    .Where(new Published())
    .WhereIf(request.Title is not null, course => course.Title == request.Title)
    .WhereIf(request.TeacherId is not null, course => course.TeacherId == request.TeacherId)
    .WhereIf(request.OnlyPopular, new Popular(minimum: 10));
```

The condition is a question about the request, not about a row, and it is answered before the query
is built — a `WhereIf` that does not apply returns the same query object it was given, so nothing
reaches the database. There is an `IEnumerable<T>` overload of each for the same code over a list.

---

## Cursor pagination

A page remembers **where it stopped**, not how far it got. The next page is an index seek —
`WHERE (CreatedAt, Id) < (…)` — instead of an `OFFSET` that walks every row it skips and shifts
under you the moment someone inserts a row.

A row joins in by exposing the two columns the order is built on:

```csharp
using i26.Core.Pagination;

public sealed record CourseRow : ICursorPageable<CourseId>
{
    public required CourseId Id { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string Title { get; init; }
}
```

The timestamp is what the page is ordered by; the id breaks ties between rows created in the same
instant. Without the tie-breaker a page can repeat a row or skip one, which is the bug this design
exists to avoid.

**The tie-breaker is whatever the row's id already is.** A typed id works because it is comparable
and parsable — the keyset predicate reaches SQL as a comparison on the column, and the cursor
carries the id in its own textual form, prefix included. A row with no typed id says `Guid`:

```csharp
public sealed record NoteRow : ICursorPageable   // the same as ICursorPageable<Guid>
{
    public required Guid Id { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
```

### Entity Framework Core

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

### Dapper

Typed ids need one registration, at startup, since Dapper keeps its handlers in static state:

```csharp
TypedIdDapperExtensions.AddTypedIdHandlers(typeof(CourseId).Assembly);
```

Without it, a query selecting an id column into a typed id property fails while materializing —
Dapper has no conversion to fall back on. Paging is the same cursor and the same response as the
ORM side, for the query that outgrew it:

```csharp
using i26.Dapper.Pagination;

var page = await connection.ToPagedResponseAsync<CourseRow, CourseId>(
    """
    SELECT c."Id", c."Title", c."CreatedAt"
    FROM courses c
    JOIN enrollments e ON e."CourseId" = c."Id"
    WHERE c."TenantId" = @TenantId
    """,
    request,
    new { TenantId = tenantId },
    cancellationToken: ct);
```

The registration above is what turns the cursor's id back into the prefixed text the column holds,
so it is not optional once the tie-breaker is a typed id.

Your query is wrapped as a derived table and the keyset predicate, the ordering and the limit go
around it, so it only has to select the two ordering columns and filter. The column names are
arguments — `createdAtColumn`, `idColumn` — because they are written into the statement as
identifiers, which no parameter can stand in for; everything else travels as a parameter.

The paging clause is `LIMIT`, which Postgres, SQLite and MySQL take. On SQL Server, write the outer
query yourself and build the page with `CursorPage.From` — the cursor and the response are the same
either way.

### What it costs

| | |
| --- | --- |
| Rows read | `Limit + 1` — the extra row is what answers `HasNext` exactly, and it is dropped before the page is returned |
| Queries | one, unless you ask for the total |
| `Total` | **off by default.** It is a second query counting the whole matching set, which is the very cost cursor paging exists to avoid. Turn it on for the screens that show a count |
| Limit | clamped into `[1, maxLimit]`, 100 by default, so one caller cannot ask the database for everything |
| Cursor | base64url, so it survives a query string without escaping |

Give the database an index on `(CreatedAt DESC, Id DESC)`, with whatever the query filters by in
front of it, and the seek stays a seek.

### Ordering by something else

For a list sorted by name rather than by creation, the same idea holds with a different key.
`Cursor.EncodeKeyed` and `Cursor.TryDecodeKeyed` carry an arbitrary sort key alongside the id — the
id is length-prefixed and the key takes the rest, because there is no separator a sort key is
guaranteed not to contain and no width an id is guaranteed to have. The query is yours to write;
`CursorPage.From` builds the page from the rows you read.

---

## ASP.NET Core

### Problem responses

`ProblemResults.Problem` turns a failed result into `application/problem+json`, per RFC 9457:

```csharp
return result.Match(Results.Ok, ProblemResults.Problem);          // Result<T>
return result.Match(() => Results.Ok(), ProblemResults.Problem);  // Result
```

Both sides of the fold have to answer the same type, and both of these answer `IResult`. The
no-value form needs the lambda because `Results.Ok` has an optional parameter, and a method group
with one does not convert to `Func<IResult>`.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "course.notFound",
  "status": 404,
  "detail": "Course not found",
  "code": "course.notFound"
}
```

`title` and the `code` extension carry the machine-readable code; `detail` is whatever the
translator had to say, and is left out entirely when it had nothing. A `ValidationError` adds every
individual failure, each described on its own — an entry of `errors` uses the same two member names
as the root document, because it is the same thing one level down:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "validation.general",
  "status": 400,
  "code": "validation.general",
  "errors": [
    { "code": "course.title.required", "detail": "Title is required" },
    { "code": "course.title.tooLong", "detail": "Title is longer than 200 characters" }
  ]
}
```

`Error.Metadata` never reaches the response — it is internal diagnostics.

### Endpoints

Each endpoint declares its own route, next to its handler:

```csharp
using i26.AspNetCore.Endpoints;

internal sealed class PublishCourse : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("courses/{id}/publish", async (
                [FromRoute] CourseId id,
                [FromServices] ICommandHandler<PublishCourseCommand, Course> handler,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(new PublishCourseCommand(id), ct);

                return result.Match(Results.Ok, ProblemResults.Problem);
            })
            .RequireAuthorization()
            .WithTags("Courses")
            .ProducesProblem(CourseErrors.NotFound, CourseErrors.AlreadyPublished);
    }
}
```

`AddEndpoints` finds them, `MapEndpoints` maps them onto whatever builder you call it on — the
application, or a group whose prefix and conventions they all inherit. Forgetting `AddEndpoints`
throws instead of quietly starting an API with no routes.

### Declaring what can go wrong

`ProducesProblem` puts the statuses in the OpenAPI document, taking the errors themselves so the
document follows the code:

```csharp
.ProducesProblem(CourseErrors.NotFound, CourseErrors.AlreadyPublished)   // 404 and 409
.ProducesProblem(ErrorType.Unauthorized)                                 // or by kind of failure
```

Statuses are deduplicated, and it works on a route group as well as on a single endpoint.

### Global exception handler

Anything that escapes a handler comes back in the same shape as a business failure:

- a `BadHttpRequestException` — malformed JSON, a value that would not bind — becomes **400** with
  the code `request.{field}.invalid`, the field taken from the JSON path and passed as an argument;
- anything else becomes **500** with the code `general.failure`.

The exception message reaches the client **only in Development**. Anywhere else a 500 carries
nothing but its code — messages routinely spell out connection strings, file paths and SQL. The full
exception is always in the log.

Three codes to add to your resources: `general.failure`, `request.body.invalid` and
`request.{field}.invalid`.

---

## Putting it together

```csharp
using System.Reflection;
using i26.AspNetCore.Diagnostics;
using i26.AspNetCore.Endpoints;
using i26.Core.Ids.Json;
using i26.Core.Results;
using i26.Cqrs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());
builder.Services.AddHandlers(typeof(PublishCourseCommand).Assembly);
builder.Services.AddSingleton<IErrorTranslator, ResourceErrorTranslator>();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new TypedIdJsonConverterFactory()));

var app = builder.Build();

app.UseExceptionHandler();

app.MapGroup("v1").MapEndpoints();

await app.RunAsync();
```

And in the `DbContext`:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    => builder.ApplyTypedIdConventions(typeof(Course).Assembly);
```

> **Naming note.** If your project has a namespace ending in `Results` — say `Api.Results` — as a
> sibling of the one holding your endpoints, the identifier `Results` in those files resolves to
> that namespace instead of `Microsoft.AspNetCore.Http.Results`. Alias it, and put the alias
> **inside** the namespace declaration:
>
> ```csharp
> namespace Api.Endpoints.Courses;
>
> using Results = Microsoft.AspNetCore.Http.Results;
> ```
>
> Above the declaration it would sit at compilation-unit scope, which is searched last and loses to
> the enclosing namespace's member. `TypedResults` sidesteps the name, but its concrete return types
> do not unify with `ProblemResults.Problem` in a single `Match`, so that fold would have to be
> typed as `IResult` by hand.

---

## Error types

`ErrorType` decides the status, so the application layer never mentions HTTP. Six cover almost
everything:

| Kind | Status | When |
| --- | --- | --- |
| `Validation` | 400 | The input did not pass validation |
| `Problem` | 400 | Well formed, but not allowed right now |
| `Forbidden` | 403 | The caller is known but not allowed |
| `NotFound` | 404 | The resource does not exist |
| `Conflict` | 409 | The state conflicts with the operation |
| `Failure` | 500 | Something broke that the caller cannot fix |

<details>
<summary>The other 34 cover the rest of the 4xx and 5xx range</summary>

`Unauthorized` 401 · `PaymentRequired` 402 · `MethodNotAllowed` 405 · `NotAcceptable` 406 ·
`ProxyAuthenticationRequired` 407 · `RequestTimeout` 408 · `Gone` 410 · `LengthRequired` 411 ·
`PreconditionFailed` 412 · `ContentTooLarge` 413 · `UriTooLong` 414 · `UnsupportedMediaType` 415 ·
`RangeNotSatisfiable` 416 · `ExpectationFailed` 417 · `MisdirectedRequest` 421 ·
`UnprocessableContent` 422 · `Locked` 423 · `FailedDependency` 424 · `TooEarly` 425 ·
`UpgradeRequired` 426 · `PreconditionRequired` 428 · `TooManyRequests` 429 ·
`RequestHeaderFieldsTooLarge` 431 · `UnavailableForLegalReasons` 451 · `NotImplemented` 501 ·
`BadGateway` 502 · `ServiceUnavailable` 503 · `GatewayTimeout` 504 · `HttpVersionNotSupported` 505 ·
`VariantAlsoNegotiates` 506 · `InsufficientStorage` 507 · `LoopDetected` 508 · `NotExtended` 510 ·
`NetworkAuthenticationRequired` 511

They exist so an adapter can relay an exact status — a gateway propagating an upstream 502, a rate
limiter, a payment wall — without inventing a mapping of its own. The numeric values of the enum are
explicit and stable; append, never renumber.

</details>

Factories exist for the common ones (`Error.Unauthorized`, `Error.TooManyRequests`,
`Error.ServiceUnavailable`, …); `Error.Create(code, type)` reaches the rest.

---

## Compatibility

Multi-targets **net8.0**, **net9.0** and **net10.0**. On .NET 9 and later, UUIDv7 comes from
`Guid.CreateVersion7()`; on .NET 8 it is generated in the same layout by hand.

Everything is annotated for nullable reference types, ships XML documentation, and builds with
warnings as errors.

The parts that reach for reflection — the assembly scans behind `ApplyTypedIdConventions`,
`AddTypedIdHandlers`, `TypedIdPrefix.ValidateAll` and the JSON converter factory — carry
`RequiresUnreferencedCode` and `RequiresDynamicCode`, so a trimmed or AOT-published application is
told which calls it has to account for rather than finding out at startup. Everything on a request
path — formatting, parsing, comparing, the generated members — is free of both.

## Building

```bash
dotnet build
dotnet test
dotnet format          # the formatting CI verifies
```

Every push and pull request runs the same three gates on Ubuntu and on Windows: a Release build
with warnings as errors, the tests on each of the three target frameworks, and a formatting check.
Packages are built and attached to the run so a branch can be tried out before it is released.

392 tests run against all three target frameworks, except the generator's own, which run once
because a generator lives inside the compiler rather than on a target runtime. The Entity Framework tests execute against an
in-memory SQLite database, including the DDL with the `"C"` collation, and the paging tests walk
every page of a seeded table on both the Entity Framework and the Dapper side, checking that no row
is repeated or skipped; the ASP.NET Core tests build a real host and read back the routes and the
JSON that reaches the wire. The snippets in this file
are not decorative — the folds above are compiled and executed by `DocumentedUsageTests`.

## License

[MIT](LICENSE) © Tiago Bittencourt
