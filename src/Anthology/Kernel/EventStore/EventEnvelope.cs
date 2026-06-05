namespace Anthology.Kernel.EventStore;

public sealed record EventEnvelope(
    Guid StreamId,
    string StreamType,
    int Version,
    IDomainEvent Event,
    EventMetadata Metadata,
    Guid? UserId = null,
    Guid? TitleId = null);
