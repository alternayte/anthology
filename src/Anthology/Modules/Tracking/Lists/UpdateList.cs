using Anthology.Kernel;
using Deedbox;
using FluentValidation;

namespace Anthology.Modules.Tracking;

public static class UpdateList
{
    public sealed record Command(string? Name, string? Description, bool DescriptionProvided, ListVisibility? Visibility,
        Guid UserId, Guid ListId, DateTimeOffset At)
        : ICommand<Result<CuratedListDto>>, ICuratedListCommand;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
            RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
        }
    }

    public sealed class Handler(IEventStore store) : ICommandHandler<Command, Result<CuratedListDto>>
    {
        public Task<Result<CuratedListDto>> Handle(Command command, CancellationToken ct) =>
            CuratedList.Execute(store, command, ct);
    }
}
