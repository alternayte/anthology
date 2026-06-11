using System.Security.Claims;
using Anthology.Kernel;

namespace Anthology.Modules.Recommendations;

public sealed record FeedbackRequest(Guid TitleId, string Signal);

internal static class SnakeCaseEnum
{
    public static bool TryParse<TEnum>(string value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        foreach (var v in Enum.GetValues<TEnum>())
        {
            var snake = System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString());
            if (string.Equals(snake, value, StringComparison.OrdinalIgnoreCase))
            {
                result = v;
                return true;
            }
        }
        return false;
    }
}

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
        {
            if (!SnakeCaseEnum.TryParse<FeedbackSignal>(request.Signal, out var signal))
                return Results.Problem("Unknown feedback signal.", statusCode: 400, title: "signal.unknown");

            var ack = await handler.Handle(new SubmitFeedback.Command(user.UserId(), request.TitleId, signal), ct);
            return Results.Ok(ack);
        })
            .RequireAuthorization().WithName("submitFeedback").Produces<SubmitFeedback.FeedbackAck>();

        return app;
    }
}
