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
- [Putting it together](#putting-it-together)
- [Compatibility](#compatibility)
- [Building](#building)

---

## Packages

Six packages. A domain project references `i26.Core` and nothing else; everything that needs a
dependency lives in the package named after it. **Each package README is the documentation for that
package** — this page is the map.

| Package | What it solves | Depends on |
| --- | --- | --- |
| [`i26.Core`](src/i26.Core/README.md) | Typed ids and their generator, base entities, `Result`/`Error`, cursor paging, domain events, specifications, the query seam | nothing outside the BCL |
| [`i26.Cqrs`](src/i26.Cqrs/README.md) | Commands, queries, their handlers and the registration that finds them | `Microsoft.Extensions.DependencyInjection.Abstractions` |
| [`i26.EntityFrameworkCore`](src/i26.EntityFrameworkCore/README.md) | Typed id conventions, cursor paging over `IQueryable`, domain event collection on save | `Microsoft.EntityFrameworkCore.Relational` |
| [`i26.Dapper`](src/i26.Dapper/README.md) | Typed id handlers, cursor paging over a hand-written query | `Dapper` |
| [`i26.Hosting`](src/i26.Hosting/README.md) | Domain events handled off the request, as a hosted service | `Microsoft.Extensions.Hosting.Abstractions` |
| [`i26.AspNetCore`](src/i26.AspNetCore/README.md) | Problem responses, endpoint discovery, global exception handler | ASP.NET Core shared framework |

Some subjects are split across two packages, because the seam is the point: raising a domain event
is `i26.Core`, running its handlers is `i26.Cqrs`, collecting them as rows are saved is
`i26.EntityFrameworkCore`, and doing that off the request is `i26.Hosting`. Each README says where
its half of the story continues.

## Installing

```bash
dotnet add package i26.Core
dotnet add package i26.Cqrs
dotnet add package i26.EntityFrameworkCore
dotnet add package i26.Dapper
dotnet add package i26.Hosting
dotnet add package i26.AspNetCore
```

Take only what you need: a domain project takes `i26.Core` and the rest follows the dependency it
carries. Every version on nuget.org was published from a tag by
[the release workflow](.github/workflows/release.yml).

---

## Putting it together

**Nothing registers itself.** A package cannot see your container, and would not know which
assemblies to scan or which context is yours if it could. Every call below is one you make, and none
of them is required by another package — an application with no Entity Framework skips those lines
and everything else still works.

```csharp
using System.Reflection;
using i26.AspNetCore.Diagnostics;
using i26.AspNetCore.Endpoints;
using i26.Core.Ids.Json;
using i26.Core.Results;
using i26.Cqrs;
using i26.EntityFrameworkCore.DomainEvents;
using i26.EntityFrameworkCore.Ids;
using i26.EntityFrameworkCore.Queries;
using i26.Hosting.DomainEvents;

var builder = WebApplication.CreateBuilder(args);

// i26.Cqrs — every handler in the assembly, and the domain event plumbing
builder.Services.AddHandlers(typeof(PublishCourseCommand).Assembly);
builder.Services.AddDomainEvents();

// i26.Hosting — optional, and it takes over the dispatcher AddDomainEvents just registered
builder.Services.AddBackgroundDomainEvents();

// i26.EntityFrameworkCore — the executor an application layer awaits queries through
builder.Services.AddEfCoreAsyncQueries();

builder.Services.AddDbContext<AppDbContext>((provider, options) => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Default"))
    .UseDomainEvents(provider));          // ← the one that fails silently if you forget it

// i26.AspNetCore
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

Two more that are not container registrations. Dapper keeps its handlers in static state, so its
one call goes at startup and takes no services:

```csharp
TypedIdDapperExtensions.AddTypedIdHandlers(typeof(Course).Assembly);   // i26.Dapper
TypedIdPrefix.ValidateAll(typeof(Course).Assembly);                    // optional: fails fast on a bad or duplicate prefix
```

`i26.Core` asks for nothing. `Result`, the typed ids, specifications and `WhereIf` are types and
extension methods, with no registration behind them.

### What a missing call looks like

Almost all of them fail immediately and say so: without `AddEfCoreAsyncQueries` the
`IAsyncQueryExecutor` does not resolve, without `AddDomainEvents` the `UseDomainEvents` call throws
naming the method you skipped, without `AddHandlers` the endpoint cannot resolve its handler.

**`UseDomainEvents` is the exception, and the one to not forget.** Leave it off and the application
starts, saves, and raises events that nobody ever collects — an entity raising an event that nothing
takes is indistinguishable from an entity that raised nothing.

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

---

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

---

## License

[MIT](LICENSE) © Tiago Bittencourt
