# i26.Cqrs

Commands, queries and their handlers, with no mediator in the middle. A caller asks the container
for the handler of the exact request it means, which the compiler checks and a reader can follow to
its declaration.

```bash
dotnet add package i26.Cqrs
```

## What is in here

**The contracts.** The response type lives on the request, so the handler and every call site agree
on it without anyone restating it.

```csharp
public sealed record PublishCourseCommand(CourseId Id) : ICommand;
public sealed record GetCourseQuery(CourseId Id) : IQuery<CourseResponse>;

internal sealed class PublishCourseHandler(ICourseRepository courses)
    : ICommandHandler<PublishCourseCommand>
{
    public async Task<Result> HandleAsync(PublishCourseCommand command, CancellationToken ct = default)
        => await courses.FindAsync(command.Id, ct) is { } course
            ? course.Publish()
            : CourseErrors.NotFound;
}
```

**The registration**, which finds every handler in an assembly — internal ones included, since a
handler is an implementation detail of the application layer.

```csharp
services.AddHandlers(typeof(DependencyInjection).Assembly);
```

Two handlers for one command is refused, naming both, rather than resolved to whichever was scanned
last. A domain event takes as many handlers as it finds, because that is what an event is for.

**Domain event dispatch.** `AddDomainEvents()` registers the queue and a dispatcher that runs the
handlers in process, in the scope that published. Handing them to a queue or an outbox instead is
your own `IDomainEventDispatcher`; handling them off the request is
[i26.Hosting](https://www.nuget.org/packages/i26.Hosting).

Nothing is decorated for you — validation, logging and transactions are decisions about your
application. The registration is the plain closed-generic kind, so
[Scrutor](https://github.com/khellang/Scrutor) wraps it the usual way.

## What it drags in

`Microsoft.Extensions.DependencyInjection.Abstractions`, and i26.Core.

## Documentation

[Commands and queries](https://github.com/tiago-bitten/i26#commands-and-queries) and
[domain events](https://github.com/tiago-bitten/i26#domain-events) in the repository README.
