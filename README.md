# i26

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
- [ASP.NET Core](#aspnet-core)
- [Putting it together](#putting-it-together)
- [Error types](#error-types)
- [Compatibility](#compatibility)
- [Building](#building)

---

## Packages

| Package | What it holds | Depends on |
| --- | --- | --- |
| `i26.Core` | Typed ids, UUIDv7, Crockford base32, the JSON converter, `Result`/`Error` | nothing outside the BCL |
| `i26.EntityFrameworkCore` | Value converter, comparer and model conventions for typed ids | `Microsoft.EntityFrameworkCore.Relational` |
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
dotnet add package i26.EntityFrameworkCore
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

Each entity gets a `readonly record struct` with its prefix baked in. Everything delegates to the
helpers, so there is nothing to get wrong:

```csharp
using i26.Core.Ids;

public readonly record struct CourseId(Guid Value) : ITypedId<CourseId>
{
    public static string Prefix => "crs";

    public static CourseId FromGuid(Guid value) => new(value);
    public static CourseId New() => TypedId.New<CourseId>();

    public override string ToString() => TypedId.Format(this);

    public static CourseId Parse(string s, IFormatProvider? _ = null) => TypedId.Parse<CourseId>(s);

    public static bool TryParse(string? s, IFormatProvider? _, out CourseId result)
        => TypedId.TryParse(s, out result);
}
```

`CourseId` and `StudentId` are different types. Passing one where the other is expected does not
compile — which is the whole point.

### The prefix rule

**Up to three lowercase letters.** The prefix shows up in every id, every log line and every URL,
and three characters are enough to tell entities apart at a glance — `usr`, `ord`, `crs`, `inv`.

It is checked, once per id type, the first time one is formatted or parsed. An empty prefix, an
uppercase letter, a digit, an underscore or a fourth character stops the type with a message saying
which rule it broke. `TypedIdPrefix.Validate<CourseId>()` runs the same check on demand, for a
startup assertion or a test that sweeps every id in the assembly.

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
TypedId.GetTimestamp(id);          // when it was created, to the millisecond
TypedId.Compare(first, second);    // chronological order

CourseId.TryParse("std_01h455vb4pex5vsknk084sn02q", null, out _);   // false: wrong prefix
```

Parsing is strict: exact length, exact prefix, lowercase only, and no `i`, `l`, `o` or `u` — every
id has exactly one textual form.

### Ordering

The encoding preserves order, so sorting the strings sorts by creation time:

```csharp
ids.Select(id => id.ToString()).Order(StringComparer.Ordinal);   // chronological
```

Use `TypedId.Compare` rather than `Guid.CompareTo` when you need the guarantee in code: it compares
the bytes big-endian, which is the same order the database column uses. Note that
`Guid.ToByteArray()` is little-endian for the first three fields and *does not* preserve it.

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

### The decisions behind it

| Decision | Why |
| --- | --- |
| Ids are created by the application, never by the database | The entity is whole before it is saved, and nothing needs a round trip to learn its own id |
| UUIDv7 underneath | 48 bits of timestamp up front means new rows land at the right end of the B-tree, unlike a random UUIDv4 |
| Stored as `text`, prefix included | The id in a log line, in a URL and in a `WHERE` clause is one copy-paste. The extra bytes buy ergonomics, and that trade was made on purpose |
| Collation `"C"` | Sorts byte by byte, so database order equals creation order and the index does not depend on the server locale |
| One value object per entity | The compiler catches a `StudentId` passed where a `CourseId` belongs |

### Referencing another service's ids

Declare a value object with the owning service's prefix and **no `New()`** — only the service that
owns a prefix mints ids with it:

```csharp
public readonly record struct AuthUserId(Guid Value) : ITypedId<AuthUserId>
{
    public static string Prefix => "usr";   // the prefix the auth service mints, not ours
    public static AuthUserId FromGuid(Guid value) => new(value);

    public override string ToString() => TypedId.Format(this);
    public static AuthUserId Parse(string s, IFormatProvider? _ = null) => TypedId.Parse<AuthUserId>(s);
    public static bool TryParse(string? s, IFormatProvider? _, out AuthUserId result)
        => TypedId.TryParse(s, out result);
}
```

It is a typed reference, not a foreign key.

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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());
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

## Building

```bash
dotnet build
dotnet test
```

207 tests run against all three target frameworks. The Entity Framework tests execute against an
in-memory SQLite database, including the DDL with the `"C"` collation; the ASP.NET Core tests build
a real host and read back the routes and the JSON that reaches the wire. The snippets in this file
are not decorative — the folds above are compiled and executed by `DocumentedUsageTests`.

## License

[MIT](LICENSE) © Tiago Bittencourt
