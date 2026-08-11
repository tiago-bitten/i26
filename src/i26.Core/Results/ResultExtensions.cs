namespace i26.Core.Results;

/// <summary>
/// Combinators for chaining operations that return a <see cref="Result"/> without an
/// <c>if (IsFailure) return</c> after every step. Every one of them short-circuits: once a result
/// is a failure, the following steps do not run and the original error is carried through.
/// </summary>
public static class ResultExtensions
{
    /// <summary>Folds both branches into a single value.</summary>
    /// <typeparam name="TOut">The type both branches produce.</typeparam>
    /// <param name="result">The result to fold.</param>
    /// <param name="onSuccess">Called when the result succeeded.</param>
    /// <param name="onFailure">Called with the failed result.</param>
    /// <returns>Whatever the branch that ran produced.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <remarks>
    /// This is the usual last step of an endpoint:
    /// <code>
    /// return result.Match(Results.Ok, ProblemResults.Problem);
    /// </code>
    /// </remarks>
    public static TOut Match<TOut>(
        this Result result,
        Func<TOut> onSuccess,
        Func<Result, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result.IsSuccess ? onSuccess() : onFailure(result);
    }

    /// <summary>Folds both branches into a single value.</summary>
    /// <typeparam name="TIn">The value type of the result.</typeparam>
    /// <typeparam name="TOut">The type both branches produce.</typeparam>
    /// <param name="result">The result to fold.</param>
    /// <param name="onSuccess">Called with the value when the result succeeded.</param>
    /// <param name="onFailure">Called with the failed result.</param>
    /// <returns>Whatever the branch that ran produced.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static TOut Match<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, TOut> onSuccess,
        Func<Result<TIn>, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result.IsSuccess ? onSuccess(result.Value) : onFailure(result);
    }

    /// <summary>Transforms the value of a successful result.</summary>
    /// <typeparam name="TIn">The incoming value type.</typeparam>
    /// <typeparam name="TOut">The outgoing value type.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="map">The transformation; not called on failure.</param>
    /// <returns>A result carrying the mapped value, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(map);

        return result.IsSuccess ? map(result.Value) : result.Error;
    }

    /// <summary>Runs the next operation only when this one succeeded.</summary>
    /// <param name="result">The result to chain from.</param>
    /// <param name="next">The operation to run on success.</param>
    /// <returns>The next result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static Result Bind(this Result result, Func<Result> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);

        return result.IsSuccess ? next() : result;
    }

    /// <summary>Runs the next value-producing operation only when this one succeeded.</summary>
    /// <typeparam name="TOut">The value type the next operation produces.</typeparam>
    /// <param name="result">The result to chain from.</param>
    /// <param name="next">The operation to run on success.</param>
    /// <returns>The next result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static Result<TOut> Bind<TOut>(this Result result, Func<Result<TOut>> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);

        return result.IsSuccess ? next() : result.Error;
    }

    /// <summary>Runs the next operation with the value only when this one succeeded.</summary>
    /// <typeparam name="TIn">The value type of the result.</typeparam>
    /// <param name="result">The result to chain from.</param>
    /// <param name="next">The operation to run on success.</param>
    /// <returns>The next result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static Result Bind<TIn>(this Result<TIn> result, Func<TIn, Result> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);

        return result.IsSuccess ? next(result.Value) : result;
    }

    /// <summary>Runs the next value-producing operation with the value only when this one succeeded.</summary>
    /// <typeparam name="TIn">The incoming value type.</typeparam>
    /// <typeparam name="TOut">The outgoing value type.</typeparam>
    /// <param name="result">The result to chain from.</param>
    /// <param name="next">The operation to run on success.</param>
    /// <returns>The next result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);

        return result.IsSuccess ? next(result.Value) : result.Error;
    }

    /// <summary>Runs a side effect on success and passes the result along untouched.</summary>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onSuccess">The side effect.</param>
    /// <returns>The same result.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static Result Tap(this Result result, Action onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        if (result.IsSuccess)
        {
            onSuccess();
        }

        return result;
    }

    /// <summary>Runs a side effect with the value on success and passes the result along untouched.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onSuccess">The side effect.</param>
    /// <returns>The same result.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static Result<T> Tap<T>(this Result<T> result, Action<T> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        if (result.IsSuccess)
        {
            onSuccess(result.Value);
        }

        return result;
    }

    /// <summary>Fails a successful result when a condition does not hold.</summary>
    /// <param name="result">The result to check.</param>
    /// <param name="predicate">The condition; not evaluated on failure.</param>
    /// <param name="error">The failure to produce when the condition does not hold.</param>
    /// <returns>The original result, or a failure carrying <paramref name="error"/>.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static Result Ensure(this Result result, Func<bool> predicate, Error error)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);

        if (result.IsFailure)
        {
            return result;
        }

        return predicate() ? result : error;
    }

    /// <summary>Fails a successful result when its value does not satisfy a condition.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result to check.</param>
    /// <param name="predicate">The condition; not evaluated on failure.</param>
    /// <param name="error">The failure to produce when the condition does not hold.</param>
    /// <returns>The original result, or a failure carrying <paramref name="error"/>.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);

        if (result.IsFailure)
        {
            return result;
        }

        return predicate(result.Value) ? result : error;
    }
}
