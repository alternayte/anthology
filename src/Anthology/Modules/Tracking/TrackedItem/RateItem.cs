using Anthology.Kernel;
using Deedbox;
using FluentValidation;

namespace Anthology.Modules.Tracking;

public static class RateItem
{
    public sealed record Command(int Rating, DateTimeOffset At, Guid UserId = default, Guid TitleId = default)
        : ICommand<Result<TrackedItemDto>>, ITrackingCommand;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Rating).InclusiveBetween(1, 10);
        }
    }

    public sealed class Handler(IEventStore store) : ICommandHandler<Command, Result<TrackedItemDto>>
    {
        public Task<Result<TrackedItemDto>> Handle(Command command, CancellationToken ct) =>
            TrackedItem.Execute(store, command, ct);
    }
}
