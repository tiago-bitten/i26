namespace i26.Core.Results;

/// <summary>
/// Asynchronous counterparts of <see cref="ResultExtensions"/>, so a chain does not have to be
/// broken apart just because one step is awaited.
/// </summary>
/// <remarks>
/// Every continuation awaits with <c>ConfigureAwait(false)</c>: this is library code and must not
/// capture the caller's synchronization context.
/// </remarks>
public static class ResultAsyncExtensions
{
    /// <summary>Awaits the result and folds both branches into a single value.</summary>
    /// <typeparam name="TOut">The type both branches produce.</typeparam>
    /// <param name="resultTask">The pending result.</param>
    /// <param name="onSuccess">Called when the result succeeded.</param>
    /// <param name="onFailure">Called with the failed result.</param>
    /// <returns>Whatever the branch that ran produced.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static async Task<TOut> MatchAsync<TOut>(
        this Task<Result> resultTask,
        Func<TOut> onSuccess,
        Func<Result, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(resultTask);

        var result = await resultTask.ConfigureAwait(false);
        return result.Match(onSuccess, onFailure);
    }

    /// <summary>Awaits the result and folds both branches into a single value.</summary>
    /// <typeparam name="TIn">The value type of the result.</typeparam>
    /// <typeparam name="TOut">The type both branches produce.</typeparam>
    /// <param name="resultTask">The pending result.</param>
    /// <param name="onSuccess">Called with the value when the result succeeded.</param>
    /// <param name="onFailure">Called with the failed result.</param>
    /// <returns>Whatever the branch that ran produced.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static async Task<TOut> MatchAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, TOut> onSuccess,
        Func<Result<TIn>, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(resultTask);

        var result = await resultTask.ConfigureAwait(false);
        return result.Match(onSuccess, onFailure);
    }

    /// <summary>Awaits the result and transforms its value.</summary>
    /// <typeparam name="TIn">The incoming value type.</typeparam>
    /// <typeparam name="TOut">The outgoing value type.</typeparam>
    /// <param name="resultTask">The pending result.</param>
    /// <param name="map">The transformation; not called on failure.</param>
    /// <returns>A result carrying the mapped value, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(resultTask);

        var result = await resultTask.ConfigureAwait(false);
        return result.Map(map);
    }

    /// <summary>Transforms the value of a successful result with an asynchronous projection.</summary>
    /// <typeparam name="TIn">The incoming value type.</typeparam>
    /// <typeparam name="TOut">The outgoing value type.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="map">The transformation; not called on failure.</param>
    /// <returns>A result carrying the mapped value, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Task<TOut>> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(map);

        if (result.IsFailure)
        {
            return result.Error;
        }

        return await map(result.Value).ConfigureAwait(false);
    }

    /// <summary>Runs the next asynchronous operation only when this one succeeded.</summary>
    /// <param name="result">The result to chain from.</param>
    /// <param name="next">The operation to run on success.</param>
    /// <returns>The next result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static async Task<Result> BindAsync(this Result result, Func<Task<Result>> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);

        return result.IsSuccess ? await next().ConfigureAwait(false) : result;
    }

    /// <summary>Runs the next asynchronous operation with the value only when this one succeeded.</summary>
    /// <typeparam name="TIn">The value type of the result.</typeparam>
    /// <param name="result">The result to chain from.</param>
    /// <param name="next">The operation to run on success.</param>
    /// <returns>The next result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static async Task<Result> BindAsync<TIn>(this Result<TIn> result, Func<TIn, Task<Result>> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);

        return result.IsSuccess ? await next(result.Value).ConfigureAwait(false) : result;
    }

    /// <summary>Runs the next asynchronous operation with the value only when this one succeeded.</summary>
    /// <typeparam name="TIn">The incoming value type.</typeparam>
    /// <typeparam name="TOut">The outgoing value type.</typeparam>
    /// <param name="result">The result to chain from.</param>
    /// <param name="next">The operation to run on success.</param>
    /// <returns>The next result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Task<Result<TOut>>> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);

        if (result.IsFailure)
        {
            return result.Error;
        }

        return await next(result.Value).ConfigureAwait(false);
    }

    /// <summary>Awaits the result and runs the next asynchronous operation with its value.</summary>
    /// <typeparam name="TIn">The incoming value type.</typeparam>
    /// <typeparam name="TOut">The outgoing value type.</typeparam>
    /// <param name="resultTask">The pending result.</param>
    /// <param name="next">The operation to run on success.</param>
    /// <returns>The next result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Task<Result<TOut>>> next)
    {
        ArgumentNullException.ThrowIfNull(resultTask);

        var result = await resultTask.ConfigureAwait(false);
        return await result.BindAsync(next).ConfigureAwait(false);
    }

    /// <summary>Awaits the result and runs the next synchronous operation with its value.</summary>
    /// <typeparam name="TIn">The incoming value type.</typeparam>
    /// <typeparam name="TOut">The outgoing value type.</typeparam>
    /// <param name="resultTask">The pending result.</param>
    /// <param name="next">The operation to run on success.</param>
    /// <returns>The next result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Result<TOut>> next)
    {
        ArgumentNullException.ThrowIfNull(resultTask);

        var result = await resultTask.ConfigureAwait(false);
        return result.Bind(next);
    }

    /// <summary>Runs an asynchronous side effect on success and passes the result along untouched.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onSuccess">The side effect.</param>
    /// <returns>The same result.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static async Task<Result<T>> TapAsync<T>(this Result<T> result, Func<T, Task> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        if (result.IsSuccess)
        {
            await onSuccess(result.Value).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Awaits the result and runs a side effect with its value on success.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="resultTask">The pending result.</param>
    /// <param name="onSuccess">The side effect.</param>
    /// <returns>The same result.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static async Task<Result<T>> TapAsync<T>(this Task<Result<T>> resultTask, Action<T> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(resultTask);

        var result = await resultTask.ConfigureAwait(false);
        return result.Tap(onSuccess);
    }

    /// <summary>Awaits the result and fails it when its value does not satisfy a condition.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="resultTask">The pending result.</param>
    /// <param name="predicate">The condition; not evaluated on failure.</param>
    /// <param name="error">The failure to produce when the condition does not hold.</param>
    /// <returns>The original result, or a failure carrying <paramref name="error"/>.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, bool> predicate,
        Error error)
    {
        ArgumentNullException.ThrowIfNull(resultTask);

        var result = await resultTask.ConfigureAwait(false);
        return result.Ensure(predicate, error);
    }
}
