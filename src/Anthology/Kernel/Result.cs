namespace Anthology.Kernel;

public interface IResultUnion<TSelf>
{
    bool IsError { get; }
    Error Error { get; }
    static abstract TSelf FromError(Error error);
}

public readonly struct Result<T> : IResultUnion<Result<T>>
{
    private readonly T? _value;
    private readonly Error? _error;
    private readonly bool _isError;

    private Result(T value) { _value = value; _error = null; _isError = false; }
    private Result(Error error) { _value = default; _error = error; _isError = true; }

    public bool IsError => _isError;
    public Error Error => _error ?? throw new InvalidOperationException("Result is not an error.");
    public T Value => !_isError ? _value! : throw new InvalidOperationException("Result is an error.");

    public static Result<T> FromValue(T value) => new(value);
    public static Result<T> FromError(Error error) => new(error);

    public TOut Match<TOut>(Func<T, TOut> ok, Func<Error, TOut> err) =>
        _isError ? err(_error!) : ok(_value!);

    public static implicit operator Result<T>(T value) => new(value);
    public static implicit operator Result<T>(Error error) => new(error);
}
