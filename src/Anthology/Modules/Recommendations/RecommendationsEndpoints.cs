using System.Security.Claims;
using Anthology.Kernel;

namespace Anthology.Modules.Recommendations;

public sealed record FeedbackRequest(Guid TitleId, FeedbackSignal Signal);

public static class RecommendationsEndpoints
{
    public static WebApplication MapRecommendationsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/recommendations").WithTags("Recommendations");

        group.MapGet("/for-you", async (ClaimsPrincipal user, GetForYou.Handler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(user.UserId(), ct)))
            .RequireAuthorization().WithName("getForYou").Produces<List<GetForYou.FeedRowDto>>();

        group.MapGet("/hidden", async (ClaimsPrincipal user, GetHiddenTitles.Handler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(user.UserId(), ct)))
            .RequireAuthorization().WithName("getHiddenTitles").Produces<List<GetHiddenTitles.HiddenTitleDto>>();

        group.MapPost("/feedback", async (FeedbackRequest request, ClaimsPrincipal user, SubmitFeedback.Handler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(new SubmitFeedback.Command(user.UserId(), request.TitleId, request.Signal), ct)))
            .RequireAuthorization().WithName("submitFeedback").Produces<SubmitFeedback.FeedbackAck>();

        return app;
    }
}
