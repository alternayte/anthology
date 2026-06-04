namespace Anthology.Kernel.EventStore;

public sealed class ConcurrencyConflict(Guid streamId, int expectedVersion)
    : Exception($"Concurrency conflict on stream {streamId} at expected version {expectedVersion}.")
{
    public Guid StreamId { get; } = streamId;
    public int ExpectedVersion { get; } = expectedVersion;
}
