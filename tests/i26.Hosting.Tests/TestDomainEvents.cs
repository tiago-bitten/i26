using System.Collections.Concurrent;
using i26.Core.DomainEvents;

namespace i26.Hosting.Tests;

public sealed record CoursePublished(string Title) : IDomainEvent;

public sealed record CourseArchived : IDomainEvent;

/// <summary>Singleton, so a handler running in a scope of its own can still be seen from the test.</summary>
public sealed class Recorder
{
    private readonly SemaphoreSlim _started = new(0);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _holding;

    public ConcurrentQueue<string> Handled { get; } = new();

    public ConcurrentQueue<Guid> Scopes { get; } = new();

    /// <summary>Makes the next <paramref name="count"/> handlers wait until the test lets them go.</summary>
    public void Hold(int count) => Interlocked.Exchange(ref _holding, count);

    public void Release() => _release.TrySetResult();

    public async Task RecordAsync(string what, Guid scope)
    {
        Handled.Enqueue(what);
        Scopes.Enqueue(scope);
        _started.Release();

        if (Interlocked.Decrement(ref _holding) >= 0)
        {
            await _release.Task;
        }
    }

    /// <summary>Waits for <paramref name="count"/> handlers to have started, or fails the test.</summary>
    public async Task StartedAsync(int count)
    {
        for (var started = 0; started < count; started++)
        {
            Assert.True(
                await _started.WaitAsync(TimeSpan.FromSeconds(10)),
                $"only {Handled.Count} of {count} handlers ran");
        }
    }
}

/// <summary>Scoped, so its identity says which scope the handler ran in.</summary>
public sealed class ScopeMarker
{
    public Guid Id { get; } = Guid.NewGuid();
}

internal sealed class RecordPublishedCourse(Recorder recorder, ScopeMarker scope)
    : IDomainEventHandler<CoursePublished>
{
    public Task HandleAsync(CoursePublished domainEvent, CancellationToken cancellationToken = default)
        => recorder.RecordAsync(domainEvent.Title, scope.Id);
}

/// <summary>Throws, to show that the event after it is still handled.</summary>
internal sealed class FailOnArchivedCourse : IDomainEventHandler<CourseArchived>
{
    public const string Message = "the archive could not be written";

    public Task HandleAsync(CourseArchived domainEvent, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(Message);
}
