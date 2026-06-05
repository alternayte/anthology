namespace Anthology.Kernel;

public enum ErrorKind { Validation, NotFound, Conflict, Forbidden, Unprocessable }

public sealed record Error(ErrorKind Kind, string Code, string Message)
{
    public IDictionary<string, string[]>? ValidationErrors { get; init; }

    public static Error Validation(string code, string message) => new(ErrorKind.Validation, code, message);
    public static Error Validation(IDictionary<string, string[]> errors) =>
        new(ErrorKind.Validation, "validation", "One or more validation errors occurred.") { ValidationErrors = errors };
    public static Error NotFound(string code, string message) => new(ErrorKind.NotFound, code, message);
    public static Error Conflict(string code, string message) => new(ErrorKind.Conflict, code, message);
    public static Error Forbidden(string code, string message) => new(ErrorKind.Forbidden, code, message);
    public static Error Unprocessable(string code, string message) => new(ErrorKind.Unprocessable, code, message);
}
