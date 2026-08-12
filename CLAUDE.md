# i26

Building blocks for .NET services: strongly typed identifiers, a result pattern, CQRS contracts,
cursor pagination, and the ASP.NET Core boundary that ties them together. Five packages,
multi-targeting net8.0, net9.0 and net10.0.

| Project | Holds | External dependency |
| --- | --- | --- |
| `src/i26.Core` | Typed ids, `Result`/`Error`, pagination contracts | **none** |
| `src/i26.Core.Generators` | The `[TypedId]` source generator, shipped inside i26.Core | Roslyn (compile only) |
| `src/i26.Cqrs` | `ICommand`/`IQuery` and handler registration | DI abstractions |
| `src/i26.EntityFrameworkCore` | Typed id conventions, paging over `IQueryable` | EF Core Relational |
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
dotnet test i26.sln             # 330 tests
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
  `GeneratedIdTests` would pass against either.
- **The prefix rules exist twice**: in `TypedIdPrefix` and in the generator, which targets
  netstandard2.0 and cannot reference the library it generates for. Change one, change the other.
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
`((ICursorPageable)new Row(…)).CreatedAt`, which has no translation over a projection. Build the
expression on the concrete type — see `CursorPredicate<T>`.

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
