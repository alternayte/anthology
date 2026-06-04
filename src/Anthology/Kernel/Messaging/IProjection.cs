using Anthology.Kernel.EventStore;

namespace Anthology.Kernel.Messaging;

public interface IProjection
{
    Task ApplyAsync(IReadOnlyList<EventEnvelope> events, CancellationToken ct);
}
