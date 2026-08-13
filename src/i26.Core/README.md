# i26.Core

The primitives a domain project can hold without taking a web stack or an ORM with it. This
package references **nothing outside the BCL**, by design and enforced — everything that needs a
dependency lives in a package named after it.

```bash
dotnet add package i26.Core
```

- [Typed identifiers](#typed-identifiers)
- [Base entities](#base-entities)
- [Value objects](#value-objects)
- [Result pattern](#result-pattern)
- [Error types](#error-types)
- [Domain events](#domain-events)
- [Specifications](#specifications)
- [Awaiting a query without an ORM](#awaiting-a-query-without-an-orm)
- [Cursor pagination](#cursor-pagination)

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
checked against each other while the project compiles, and **every declaration sharing the prefix is
told**, on the string it wrote — so the error is in whichever of the files you have open, and not
only in the one the compiler happened to reach second. For ids written by hand, one test in the
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
[exception handler](https://github.com/tiago-bitten/i26/blob/main/src/i26.AspNetCore/README.md#global-exception-handler)
in i26.AspNetCore, naming the offending field.

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

## Base entities

The base is generic over the id, so an entity carries the id declared for it and no other:

```csharp
using i26.Core.Entities;

public sealed class Course : Entity<CourseId>
{
    private Course() { }                                  // the persistence layer materialises with this

    public string Title { get; private set; } = string.Empty;

    public static Course Open(string title)
    {
        var course = new Course { Title = title };        // Id is already a new CourseId
        course.Raise(new CourseOpened(course.Id, title));

        return course;
    }
}
```

`Course` has a `CourseId` and `Student` has a `StudentId`, and handing one to the other does not
compile. The parameterless constructor mints the id, so an entity is born with one; an id belonging
to another service — `Minted = false` — is left unset instead, because only that service is allowed
to invent one, and `Entity(TId id)` is how you assign what it handed over.

What comes with it:

| | |
| --- | --- |
| `Id` | The typed id, minted at construction |
| `CreatedAt`, `UpdatedAt` | Stamped by the persistence layer, not by the entity — it has no clock |
| `DomainEvents`, `ClearDomainEvents()` | [The events](#domain-events) it raised, for whoever collects them |
| `Raise(…)` | Protected, so an event is raised by the behaviour that caused it and by nothing else |

**Two entities of the same type with the same id are equal**, however many times the row was loaded.
One whose id was never assigned is only equal to itself: an id nobody set identifies nothing.

And because `Id` and `CreatedAt` are already there, an entity **is** an
[`ICursorPageable<TId>`](#cursor-pagination) — paging over the entities themselves needs no
projection and no extra interface.

### Deleting without deleting

```csharp
public sealed class Course : DeletableEntity<CourseId>;
```

```csharp
var deleted = course.Delete();     // Result: entity.alreadyDeleted if it already was
var back = course.Restore();       // Result: entity.notDeleted if it never was
```

`IsDeleted` and `DeletedAt` come with it, and i26.EntityFrameworkCore has the query filter that
hides those rows and the interceptor that stamps the instant. `Delete` is virtual, so an entity with
a reason of its own has the last word:

```csharp
public override Result Delete() => HasShipped ? OrderErrors.Shipped : base.Delete();
```

None of this is required. An entity that would rather not inherit implements
[`IHasDomainEvents`](#domain-events) — a list and two members — and keeps its own shape.

---

## Value objects

An `Email` in a signature is an address that already passed. There is no public constructor, so the
only way to hold one is to have been given one that was checked:

```csharp
using i26.Core.ValueObjects;

var email = Email.Create(request.Email);      // Result<Email>

if (email.IsFailure)
{
    return email.Error;                       // email.malformed, email.tooLong, …
}

await handler.HandleAsync(new RegisterCommand(email.Value), ct);
```

It answers with a `Result` rather than throwing, so a bad address travels the way every other
refusal does — through the [result pattern](#result-pattern), out to a problem response, with a code
a translator can turn into a sentence. And the code says **which rule**, because a form telling
someone their address is too long is worth more than one telling them it is wrong:

| | |
| --- | --- |
| `email.required` | nothing was given |
| `email.tooLong` | longer than 254 characters, carrying the limit as an argument |
| `email.malformed` | no `@`, more than one, or nothing on one side |
| `email.localPart.invalid` | the part before the `@` |
| `email.domain.invalid` | the part after it |

`Email.Parse` is the same rules, throwing — for a row this application wrote itself, or a fixture.

### What it normalises, and what it refuses

**Trimmed and lowercased.** Two people typing the same address differently wrote the same address,
which is what makes equality and a unique index agree with each other. `LocalPart` and `Domain` are
there without splitting it again.

The rules are written as loops rather than as a pattern: the local part is letters, digits and
`. _ - +`, not starting, ending or doubling on a dot, up to 64 characters; the domain is letters,
digits and hyphens in labels of up to 63, with at least one dot.

That refuses things RFC 5322 allows — a quoted local part, an IP-literal domain, a host name with no
dot. It refuses them on purpose: no provider accepts them, and an address is being taken here to
send mail to. **Passing this is not a promise the address exists** — the only check for that is
sending something to it.

### Writing your own

`Email` is not a special case, it is the first one. A value object that is one string implements
`IStringValueObject<TSelf>` — four members, and everything downstream already knows what to do with
it:

```csharp
public sealed record Slug : IStringValueObject<Slug>
{
    private Slug(string value) => Value = value;

    public static int MaxLength => 80;
    public string Value { get; }

    public static Result<Slug> Create(string? value) { /* your rules */ }

    public static Slug Parse(string s, IFormatProvider? provider) { /* Create, or throw */ }
    public static bool TryParse(string? s, IFormatProvider? provider, out Slug result) { /* Create */ }
}
```

The last two come from `IParsable<TSelf>`, which the interface derives from — the same reason a
typed id binds out of a route with no registration.

That is the whole contract. i26.EntityFrameworkCore maps it with the converter and comparer it
already has, from a call that names **your** assembly, and there is no line about it anywhere in
this library — which is what a test for a value object declared outside it asserts.

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

## What it drags in

Nothing. That is the point of this one.

---

Part of [i26](https://github.com/tiago-bitten/i26#readme).
