# i26

Building blocks for .NET services: strongly typed identifiers, a result pattern, CQRS contracts,
cursor pagination, and the ASP.NET Core boundary that ties them together. Five packages,
multi-targeting net8.0, net9.0 and net10.0.

| Project | Holds | External dependency |
| --- | --- | --- |
| `src/i26.Core` | Typed ids, `Result`/`Error`, cursor paging, domain events, specifications, the query seam | **none** |
| `src/i26.Core.Generators` | The `[TypedId]` source generator, shipped inside i26.Core | Roslyn (compile only) |
| `src/i26.Cqrs` | `ICommand`/`IQuery`, handler registration, the in-process domain event dispatcher | DI abstractions |
| `src/i26.EntityFrameworkCore` | Typed id conventions, the async query backend, the domain event interceptor | EF Core Relational |
| `src/i26.Dapper` | Paging over a hand-written query | Dapper |
| `src/i26.AspNetCore` | ProblemDetails, `IEndpoint`, exception handler | ASP.NET shared framework |

Contracts are split by layer, implementations by the dependency they carry. A domain project
references `i26.Core` and nothing else.

Six projects, five packages: `i26.Core.Generators` is `IsPackable=false` and travels inside
`i26.Core` under `analyzers/dotnet/cs`. It is a separate project because Roslyn loads a generator as
netstandard2.0 and packs it outside `lib/`, which one project cannot do alongside being a runtime
library — not because it is a separate thing.

## Commands

```bash
dotnet build i26.sln            # all 5 projects x 3 target frameworks
dotnet test i26.sln             # 519 tests
dotnet build i26.sln -c Release # must end with 0 warnings
dotnet format                   # fixes what the CI formatting gate checks
```

CI (`.github/workflows/ci.yml`) runs the same on Ubuntu and Windows for every push and pull
request: Release build, tests on each target framework, `dotnet format --verify-no-changes`, then
pack. Two things follow from warnings being errors: an analyzer complaint fails CI, and so does a
NuGet advisory against anything referenced — which is why Dependabot keeps the references moving.

## Non-negotiables

- **English everywhere** — code, comments, XML docs, test names, commit messages.
- **XML docs on every public member.** `GenerateDocumentationFile` is on and warnings are errors, so
  a missing `<summary>` fails the build. Keep them short — the rules below.
- **`i26.Core` has no external dependencies.** Never add a `PackageReference` to it. Anything needing
  one goes in the package named after that dependency.
- **New code must compile on net8.0.** Guard anything newer with `#if NET9_0_OR_GREATER`, as
  `Uuid7.New` does.
- **Package references are pinned per target framework** (`Condition="'$(TargetFramework)' == …"`), so
  a net8 consumer is not dragged to a newer runtime library.
- **Never add a `Co-Authored-By` trailer to a commit.**
- **Every regex takes a timeout.** Better still, if the rule is a character class, write the loop —
  see `CursorSqlColumn`.
- **Line endings are LF**, fixed by `.gitattributes` and `.editorconfig`. CRLF in a file makes the
  formatting gate fail on Linux.

## Writing the docs

They show up in a tooltip, one member at a time. A paragraph of rationale there is noise; the same
paragraph in the README is documentation.

- **`<summary>` is one sentence** saying what the member does. Not why, not how.
- **`<remarks>` only when it changes how you call it** — an ordering guarantee, "call this once at
  startup", "off by default because". Two lines at most. Never a tutorial.
- **`<param>` and `<returns>` only when the name does not already say it.** Watch out: they are
  all-or-nothing per member. Document one parameter and CS1573 demands the rest.
- **`<exception>` only for what the caller decides on.** `ArgumentNullException` on a null argument
  is not news.
- **The reasoning goes in a `//` comment in the body**, where it reaches whoever is changing the
  code and nobody else. Examples go in the README, once.

The pass that established this took the source from 41% documentation to 25%.

## Conventions

- **Error codes** are `dot.camelCase` (`course.notFound`, `classroom.teachingLevel.required`), declared
  as fields on a static `{Entity}Errors` class, never inline at the call site. An error whose message
  needs a value is a method: `TitleTooLong(int max) => Error.Validation("course.title.tooLong", max)`.
- **`Error` carries no text.** The description is resolved at the boundary by `IErrorTranslator`.
  Identity is the code and the type — arguments and metadata are payload.
- **Typed id prefixes** are up to three lowercase letters. Longer needs
  `UsesExtendedPrefix => true` next to the prefix, and tops out at ten.
- **New ids are declared with `[TypedId("crs")]`** on a partial struct; the generator writes the
  members. The hand-written shape stays valid and the two are interchangeable — every test in
  `GeneratedIdTests` would pass against either. Interchangeable includes a plain `struct`: the
  compiler hands a `record struct` equality and hands a struct nothing, so the generator writes
  `Equals`, `GetHashCode`, `==` and `!=` for the second one.
- **An id that another service mints is `[TypedId("usr", Minted = false)]`.** No `New()` is written,
  and `TypedId.New<TId>()` throws — the convention is enforced rather than implied, because the
  generic form used to walk around a hand-written id that simply left `New()` off.
- **Anything reading the 128 bits of an id is a `Try`.** `Parse` checks the prefix and the alphabet
  and nothing else, so an id off a route can carry any bits at all: `Uuid7.GetTimestamp` checks the
  version nibble *and* the range, because 48 bits reach the year 10889 and a `DateTimeOffset` stops
  at 9999. The same rule made `Cursor.TryDecode` range-check its timestamp — a cursor is a query
  string, and `long.TryParse` succeeds long before the value names an instant.
- **The prefix rules exist twice**: in `TypedIdPrefix` and in the generator, which targets
  netstandard2.0 and cannot reference the library it generates for. `PrefixRuleTests` fails when
  the two drift.
- **Diagnostic titles and messages are ASCII.** They travel through build logs and terminals of
  unknown encoding, and one of them already lost a character to a round trip through a cp1252 tool.
  `PrefixRuleTests` asserts it.
- **Tests run against real infrastructure** — SQLite in memory for EF Core and Dapper, a real
  `WebApplication` for ASP.NET — not mocks. A test that only proves the mock was called proves nothing.
- **Test names are sentences**: `Rows_sharing_an_instant_are_still_cut_cleanly`.
- **Mapping tables get a second copy in the test**, written by hand, so a typo in the source shows up
  as a failure — see `ErrorTests.ExpectedStatusCodes`.

## Things that cost time here once

**`Results` is shadowed by any sibling namespace of that name.** In a file under `Api.Endpoints`
with an `Api.Results` next to it, `Results.Ok` binds to the namespace. The alias fixes it only
**inside** the namespace declaration — above it, it sits at compilation-unit scope and loses.

```csharp
namespace Api.Endpoints.Courses;

using Results = Microsoft.AspNetCore.Http.Results;
```

**`Match` with two method groups needs both sides to answer `IResult`.** `TypedResults.Ok` returns a
concrete type and does not unify with `ProblemResults.Problem`. And `Results.Ok` has an optional
parameter, so a method group cannot convert to `Func<IResult>` — the no-value overload takes a
lambda. `DocumentedUsageTests` compiles the shapes the README documents.

**EF Core cannot translate a member access through an interface cast.** In a method constrained to
an interface, `item => item.CreatedAt` compiles to
`((ICursorPageable<Guid>)new Row(…)).CreatedAt`, which has no translation over a projection. Build
the expression on the concrete type — see `CursorPredicate<T, TId>`.

**`IComparable<T>` is not the `<` operator, and a keyset predicate needs the operator.**
`Expression.LessThan` looks for `op_LessThan` and throws when there is none — `CompareTo` has no SQL
either way. That is why `ITypedId<TSelf>` asks for `IComparable<TSelf>` (so `Order()`, a
`SortedSet` and a generic constraint all work) while the generator additionally writes `<`, `<=`,
`>` and `>=`. `Guid` has the operators but does *not* implement `IComparisonOperators`, so that
interface cannot be the constraint.

**Paging is generic over the id, and C# will not infer that type parameter.** `ToPagedResponseAsync`
infers `T` from the receiver and nothing else, so a typed id is spelled out:
`ToPagedResponseAsync<CourseRow, CourseId>(request)`. The arity-1 overload constrained to
`ICursorPageable<Guid>` is what keeps every `Guid` caller unchanged.

**EF Core projections need an object initializer, not a constructor.** `new Row { CreatedAt = … }`
binds back to its column and can be ordered by; `new Row(…)` cannot. Constructor shapes are built
afterwards with `PagedResponse.Map`.

**SQLite has neither a date nor a uuid type.** EF Core refuses to order by `DateTimeOffset`
(`DateTimeOffsetToBinaryConverter` in the test context), and Dapper cannot map either type
(`SqliteTypeHandlers`). Postgres, the target, has both. Neither workaround belongs in the library.

**PowerShell 5.1 mangles native arguments containing double quotes.** `git commit -m "…"` with quotes
inside the message reaches git as several pathspecs. Write the message to a file and use
`git commit -F`. Its `Set-Content -Encoding utf8` also writes a BOM, which the formatting gate
rejects — use `[System.IO.File]::WriteAllText` with `UTF8Encoding($false)` when rewriting a source
file from the shell.

**A `record struct` is a `RecordDeclarationSyntax`, not a `StructDeclarationSyntax`.** A generator
predicate that only looks for the latter silently skips every record struct, and the build still
succeeds — an attributed partial struct with no members is valid C#. Match on
`TypeDeclarationSyntax`, and write tests that *use* the generated members rather than only compiling.

**The generator's pipeline is three output nodes, not one.** Two are per id and stay incremental;
only the duplicate-prefix rule pays for a `Collect()`, and only diagnostics come out of it. Keep
`Location` out of the shape the emission is keyed on, or a comment typed above a declaration
rewrites the file. `i26.Core.Generators.Tests` pins both with `trackIncrementalGeneratorSteps`.

**One attribute application per type, not one per declaration.** A type attributed on two partial
parts arrives twice, and writing the same hint name twice throws out of `AddSource` and discards
every generated id in the compilation. Dedupe over attribute applications — deduping over declaring
parts silently drops a type whose attribute sits on the part that does not sort first.

**EF Core inlines `Expression.Invoke`, so composing predicates with it is not the bug it looks
like.** Verified on EF 8 and EF 10 against SQLite: `Invoke(left, p) && Invoke(right, p)` comes out
as a plain `WHERE a AND b`, because the query pipeline removes invocations before translating. The
`Predicates.Combine` in this repository rebinds the parameter anyway — a provider without that step
would client-evaluate the lot — but the comment saying so had to be corrected once already. Do not
write "EF cannot translate this" without running it.

**An `Expression<Func<T, bool>>` used inside another expression tree is a field access, not a
predicate.** `q => q.Count(predicate)` compiles to `Queryable.Count(q, <member access>)`, and a
provider reading that tree finds an object where a quoted lambda should be. Every predicate overload
in `AsyncQueryExecutorExtensions` applies a `Where` and asks the question of what is left, which is
the same SQL and also works on the in-memory fallback. `AllAsync` is the same trick inverted —
"nothing fails it" — because `All` has no `Where` form.

**SQLite refuses `Sum` over `decimal`, and only on EF Core 8.** `SqliteQueryableAggregateMethodTranslator`
throws `NotSupportedException`; EF Core 9 and 10 translate it. A test that aggregates in the query
sums an `int`, or it passes on two target frameworks out of three.

**A deleted entity is gone from the change tracker before `SavedChanges` runs.** `AcceptAllChanges`
detaches it as part of the save, so an interceptor that collects domain events afterwards silently
loses every event raised on the way to a delete. `DomainEventInterceptor` collects in
`SavingChanges` for that reason, and a save that then fails leaves the events queued — which is
right, since the change is still pending on the context.

**Entity Framework leaves `IReadOnlyList<IDomainEvent>` out of the model on its own.** A get-only
list of an interface is neither a primitive collection nor a navigation candidate, so the
`IgnoreAny<IDomainEvent>()` convention that seemed obviously necessary — every clean-architecture
template writes `builder.Ignore(e => e.DomainEvents)` — turned out to be a no-op, and was written
and then deleted. `The_events_stay_out_of_the_model_with_nothing_configured` pins the behaviour so
that a version of EF Core changing its mind shows up as a failure and not as a mapping error in
somebody's application.

**C# 14 extension blocks compile but Rider 2025.2.x does not parse them** — it reads
`extension(Foo f)` as a constructor and reports an error on a static class. The classic `this`
parameter avoids the noise.

## Layout

```
src/                    the five packages
tests/                  one test project per package
Directory.Build.props   shared build and package metadata
nuget.config            <clear /> — the corporate feed is not needed and 401s
```
