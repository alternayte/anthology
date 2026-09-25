using Deedbox;

namespace Anthology.Kernel;

public static class Decisions
{
    /// <summary>
    /// Runs a decide function that can reject the command. A rejection appends nothing and comes back as the error.
    /// </summary>
    public static async Task<Result<ExecuteResult<TState>>> Execute<TState>(
        this IEventStore store,
        string streamId,
        Func<TState, Result<IReadOnlyList<IDomainEvent>>> decide,
        CancellationToken ct)
        where TState : IState<TState>
    {
        Error? rejected = null;
        var result = await store.Execute<TState>(streamId, state =>
        {
            var decision = decide(state);
            rejected = decision.IsError ? decision.Error : null;
            return decision.IsError ? [] : decision.Value;
        }, ct);

        return rejected is null ? result : rejected;
    }
}
