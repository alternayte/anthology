using Anthology.Kernel;
using Deedbox;

namespace Anthology.Modules.Tracking;

public static class FinishItem
{
    public sealed record Command(DateTimeOffset At, Guid UserId = default, Guid TitleId = default)
        : ICommand<Result<TrackedItemDto>>, ITrackingCommand;

    public sealed class Handler(IEventStore store) : ICommandHandler<Command, Result<TrackedItemDto>>
    {
        public Task<Result<TrackedItemDto>> Handle(Command command, CancellationToken ct) =>
            TrackedItem.Execute(store, command, ct);
    }
}
