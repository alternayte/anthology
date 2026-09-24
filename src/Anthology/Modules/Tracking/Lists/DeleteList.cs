using Anthology.Kernel;
using Deedbox;

namespace Anthology.Modules.Tracking;

public static class DeleteList
{
    public sealed record Command(Guid UserId, Guid ListId, DateTimeOffset At)
        : ICommand<Result<CuratedListDto>>, ICuratedListCommand;

    public sealed class Handler(IEventStore store) : ICommandHandler<Command, Result<CuratedListDto>>
    {
        public Task<Result<CuratedListDto>> Handle(Command command, CancellationToken ct) =>
            CuratedList.Execute(store, command, ct);
    }
}
