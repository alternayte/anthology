using System.Security.Claims;
using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;
using FluentValidation;

namespace Anthology.Modules.Tracking;

public static class RerateItem
{
    public sealed record Command(Rating Rating, DateTimeOffset At, Guid UserId = default, Guid TitleId = default)
        : ICommand<Result<TrackedItemDto>>, ITrackingCommand;

    public sealed record Request(int Rating);

    public sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Rating).InclusiveBetween(1, 10);
        }
    }

    public sealed class Handler(EventStore store, InlineProjector projector, OutboxWriter outboxWriter)
        : ICommandHandler<Command, Result<TrackedItemDto>>
    {
        public async Task<Result<TrackedItemDto>> Handle(Command command, CancellationToken ct)
        {
            var streamId = StreamId.For(command.UserId, command.TitleId);
            var (loaded, version) = await store.LoadAsync<TrackedItemState>(streamId, ct);
            var state = loaded ?? TrackedItemState.Initial;

            var result = TrackedItem.Decide(state, command);
            if (result.IsError) return Result<TrackedItemDto>.FromError(result.Error);

            var newState = result.Value.Aggregate(state, TrackedItem.Evolve);
            var meta = new EventMetadata(Guid.NewGuid(), null, command.UserId, command.At);
            var envelopes = await store.AppendAsync(
                streamId, "tracked_item", version, result.Value, newState, meta, ct,
                command.UserId, command.TitleId);

            projector.Stage(envelopes);
            outboxWriter.Stage(envelopes);

            return new TrackedItemDto(streamId, command.TitleId, newState.Status, newState.Rating);
        }
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/items/{titleId:guid}/rerate", async (
            Guid titleId,
            Request request,
            ClaimsPrincipal user,
            ICommandHandler<Command, Result<TrackedItemDto>> handler,
            CancellationToken ct) =>
            (await handler.Handle(new Command(new Rating(request.Rating), DateTimeOffset.UtcNow, user.UserId(), titleId), ct)).ToHttpResult())
            .AddEndpointFilter<ValidationFilter<Request>>()
            .RequireAuthorization();
}
