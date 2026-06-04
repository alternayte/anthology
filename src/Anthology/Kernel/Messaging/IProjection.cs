using System.Data.Common;
using Anthology.Kernel.EventStore;

namespace Anthology.Kernel.Messaging;

public interface IProjection
{
    Task ApplyAsync(IReadOnlyList<EventEnvelope> events, DbTransaction transaction, CancellationToken ct);
}
