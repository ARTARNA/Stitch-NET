namespace Stitch.Core;

public sealed class StitchResult<TValue, TError>
{
    private StitchResult(TValue? value, TError? error, bool isSuccess)
    {
        Value = value;
        Error = error;
        IsSuccess = isSuccess;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public TValue? Value { get; }
    public TError? Error { get; }

    public static StitchResult<TValue, TError> Success(TValue value) =>
        new(value, default, true);

    public static StitchResult<TValue, TError> Failure(TError error) =>
        new(default, error, false);

    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<TError, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value!) : onFailure(Error!);

    public void Match(Action<TValue> onSuccess, Action<TError> onFailure)
    {
        if (IsSuccess)
            onSuccess(Value!);
        else
            onFailure(Error!);
    }
}
