using FluentValidation;

namespace Anthology.Kernel;

public sealed class ValidationDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IValidator<TCommand>? validator = null)
    : ICommandHandler<TCommand, TResult>
    where TResult : IResultUnion<TResult>
{
    public async Task<TResult> Handle(TCommand command, CancellationToken ct)
    {
        if (validator is not null)
        {
            var result = await validator.ValidateAsync(command, ct);
            if (!result.IsValid)
                return TResult.FromError(Error.Validation(result.ToDictionary()));
        }

        return await inner.Handle(command, ct);
    }
}
