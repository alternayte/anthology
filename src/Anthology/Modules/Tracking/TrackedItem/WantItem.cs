using Anthology.Kernel;
using Deedbox;

namespace Anthology.Modules.Tracking;

public static class WantItem
{
    public sealed record Command(Guid TitleId, string TitleName, string MediaType, Guid UserId, DateTimeOffset At)
        : ICommand<Result<TrackedItemDto>>, ITrackingCommand;

    public sealed class Handler(IEventStore store) : ICommandHandler<Command, Result<TrackedItemDto>>
    {
        public Task<Result<TrackedItemDto>> Handle(Command command, CancellationToken ct) =>
            TrackedItem.Execute(store, command, ct);
    }
}
