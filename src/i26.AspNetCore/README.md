# i26.AspNetCore

The boundary: where an i26 `Result` becomes an HTTP response, and where an endpoint says what it
can answer.

```bash
dotnet add package i26.AspNetCore
```

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

## A namespace called Results

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

## What it drags in

Nothing from NuGet: it uses the ASP.NET Core shared framework, plus i26.Core.

---

Part of [i26](https://github.com/tiago-bitten/i26#readme).
