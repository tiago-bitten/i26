using i26.Core.DomainEvents;

namespace i26.Cqrs.Tests;

public sealed record CoursePublishedDomainEvent(string Title) : IDomainEvent;

/// <summary>Nobody handles this one: an event without handlers is not an error.</summary>
public sealed record CourseArchivedDomainEvent : IDomainEvent;

public sealed record CourseRenamedDomainEvent(string Title) : IDomainEvent;

/// <summary>What the handlers write to, so a test can see which ones ran and in what order.</summary>
internal sealed class CourseLog
{
    public List<string> Entries { get; } = [];
}

internal sealed class AnnounceCourseHandler(CourseLog log) : IDomainEventHandler<CoursePublishedDomainEvent>
{
    public Task HandleAsync(CoursePublishedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        log.Entries.Add($"announce:{domainEvent.Title}");

        return Task.CompletedTask;
    }
}

internal sealed class IndexCourseHandler(CourseLog log) : IDomainEventHandler<CoursePublishedDomainEvent>
{
    public Task HandleAsync(CoursePublishedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        log.Entries.Add($"index:{domainEvent.Title}");

        return Task.CompletedTask;
    }
}

/// <summary>Throws what a failing handler throws, to show it arrives unwrapped.</summary>
internal sealed class RenameCourseHandler : IDomainEventHandler<CourseRenamedDomainEvent>
{
    public const string Message = "the rename could not be applied";

    public Task HandleAsync(CourseRenamedDomainEvent domainEvent, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(Message);
}
