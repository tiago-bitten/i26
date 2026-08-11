using i26.Core.Results;
using i26.Cqrs;

namespace i26.Cqrs.Tests;

public sealed record PublishCourseCommand(string Title) : ICommand;

public sealed record CreateCourseCommand(string Title) : ICommand<Guid>;

public sealed record GetCourseQuery(Guid Id) : IQuery<string>;

/// <summary>Also proves the handlers come out of the container with their dependencies.</summary>
internal sealed class CourseTitles
{
    public string Last { get; set; } = string.Empty;
}

internal sealed class PublishCourseHandler(CourseTitles titles) : ICommandHandler<PublishCourseCommand>
{
    public Task<Result> HandleAsync(PublishCourseCommand command, CancellationToken cancellationToken = default)
    {
        titles.Last = command.Title;

        return Task.FromResult(Result.Ok());
    }
}

internal sealed class CreateCourseHandler : ICommandHandler<CreateCourseCommand, Guid>
{
    public static readonly Guid Created = Guid.Parse("01890a5d-ac96-774b-bcce-b302099a8057");

    public Task<Result<Guid>> HandleAsync(CreateCourseCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Ok(Created));
}

internal sealed class GetCourseHandler : IQueryHandler<GetCourseQuery, string>
{
    public Task<Result<string>> HandleAsync(GetCourseQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Ok("Algebra"));
}

/// <summary>Answers with a failure, to show the Result travels back untouched.</summary>
public sealed record ArchiveCourseCommand : ICommand;

internal sealed class ArchiveCourseHandler : ICommandHandler<ArchiveCourseCommand>
{
    public static readonly Error NotFound = Error.NotFound("crs.notFound");

    public Task<Result> HandleAsync(ArchiveCourseCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Failure(NotFound));
}

/// <summary>Must be skipped by the scan: there is nothing to instantiate.</summary>
internal abstract class AbstractHandler : ICommandHandler<PublishCourseCommand>
{
    public abstract Task<Result> HandleAsync(PublishCourseCommand command, CancellationToken cancellationToken = default);
}

/// <summary>Must be skipped by the scan: an open generic cannot be constructed.</summary>
internal sealed class OpenGenericHandler<TState> : IQueryHandler<GetCourseQuery, string>
{
    public Task<Result<string>> HandleAsync(GetCourseQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Ok(typeof(TState).Name));
}
