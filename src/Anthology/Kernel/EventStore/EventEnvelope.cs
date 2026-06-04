namespace Anthology.Kernel.EventStore;

public sealed record EventEnvelope(
    Guid StreamId,
    int Version,
    IDomainEvent Event,
    EventMetadata Metadata,
    Guid? UserId = null,
    Guid? TitleId = null);
