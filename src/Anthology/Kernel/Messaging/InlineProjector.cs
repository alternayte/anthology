using System.Data.Common;
using Anthology.Kernel.EventStore;

namespace Anthology.Kernel.Messaging;

public sealed class InlineProjector(IEnumerable<IProjection> projections)
{
    private readonly List<EventEnvelope> _staged = [];

    public void Stage(IReadOnlyList<EventEnvelope> envelopes) => _staged.AddRange(envelopes);

    public async Task ApplyAndSaveAsync(DbTransaction transaction, CancellationToken ct)
    {
        if (_staged.Count == 0) return;
        foreach (var projection in projections)
            await projection.ApplyAsync(_staged, transaction, ct);
        _staged.Clear();
    }
}
