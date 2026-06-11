namespace Anthology.Modules.Recommendations;

public enum FeedbackSignal
{
    Hidden,
    Seen,
    MoreLikeThis,
    // Cancels a previous Hidden/Seen — "un-hide" recorded as a new row; the table is never mutated.
    Restored
}
