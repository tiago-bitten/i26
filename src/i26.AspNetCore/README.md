# i26.AspNetCore

The boundary: where an i26 `Result` becomes an HTTP response, and where an endpoint says what it can
answer.

```bash
dotnet add package i26.AspNetCore
```

## What is in here

**Problem responses.** A failed `Result` becomes an RFC 9457 `ProblemDetails` with the status the
error type decides, so the application layer never mentions HTTP.

```csharp
var result = await handler.HandleAsync(new PublishCourseCommand(id), ct);

return result.Match(Results.NoContent, ProblemResults.Problem);
```

The description is resolved here, at the boundary, through your `IErrorTranslator` — the error
itself carries a code and arguments, which is what makes the same failure readable in two languages
without the domain knowing either.

**Endpoint discovery.** An endpoint is a class implementing `IEndpoint`, found by a scan and mapped
in one call, so adding a route is adding a file.

```csharp
services.AddEndpoints(Assembly.GetExecutingAssembly());

app.MapGroup("v1").MapEndpoints();
```

**Declaring what can go wrong**, from the errors themselves rather than from a status code typed
twice:

```csharp
app.MapPost("courses/{id}/publish", Handle)
    .ProducesProblem(CourseErrors.NotFound, CourseErrors.AlreadyPublished);
```

**A global exception handler** for what nobody planned. The message reaches the client only in
Development — anywhere else a 500 carries its code and nothing more, because exception text
routinely spells out connection strings and SQL. The full exception is always in the log.

```csharp
services.AddProblemDetails();
services.AddExceptionHandler<GlobalExceptionHandler>();

app.UseExceptionHandler();
```

## What it drags in

Nothing from NuGet: it uses the ASP.NET Core shared framework, plus i26.Core.

## Documentation

[ASP.NET Core](https://github.com/tiago-bitten/i26#aspnet-core),
[the result pattern](https://github.com/tiago-bitten/i26#result-pattern) and
[error types](https://github.com/tiago-bitten/i26#error-types) in the repository README.
