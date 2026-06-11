namespace Anthology.Modules.Recommendations;

public static class SubmitFeedback
{
    public sealed record Command(Guid UserId, Guid TitleId, FeedbackSignal Signal);

    public sealed record FeedbackAck(Guid TitleId, FeedbackSignal Signal);

    public sealed class Handler(RecommendationsDbContext db)
    {
        public async Task<FeedbackAck> Handle(Command command, CancellationToken ct)
        {
            db.Feedback.Add(new RecommendationFeedback
            {
                Id = Guid.NewGuid(),
                UserId = command.UserId,
                TitleId = command.TitleId,
                Signal = command.Signal,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
            return new FeedbackAck(command.TitleId, command.Signal);
        }
    }
}
