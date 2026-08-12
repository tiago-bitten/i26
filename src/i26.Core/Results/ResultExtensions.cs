namespace i26.Core.Results;

/// <summary>
/// Chaining for operations that return a <see cref="Result"/>, without an
/// <c>if (IsFailure) return</c> between every step.
/// </summary>
/// <remarks>
/// All of them short-circuit: once a result fails, the following steps do not run and the original
/// error is carried through.
/// </remarks>
public static class ResultExtensions
{
    /// <summary>Folds both branches into one value, usually the last line of an endpoint.</summary>
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

    /// <summary>Folds both branches into one value, with the value on the success side.</summary>
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
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(map);

        return result.IsSuccess ? map(result.Value) : result.Error;
    }

    /// <summary>Runs the next operation only when this one succeeded.</summary>
    public static Result Bind(this Result result, Func<Result> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);

        return result.IsSuccess ? next() : result;
    }

    /// <summary>Runs the next value-producing operation only when this one succeeded.</summary>
    public static Result<TOut> Bind<TOut>(this Result result, Func<Result<TOut>> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);

        return result.IsSuccess ? next() : result.Error;
    }

    /// <summary>Runs the next operation with the value, only when this one succeeded.</summary>
    public static Result Bind<TIn>(this Result<TIn> result, Func<TIn, Result> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);

        return result.IsSuccess ? next(result.Value) : result;
    }

    /// <summary>Runs the next value-producing operation with the value, only when this one succeeded.</summary>
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);

        return result.IsSuccess ? next(result.Value) : result.Error;
    }

    /// <summary>Runs a side effect on success and passes the result along untouched.</summary>
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
