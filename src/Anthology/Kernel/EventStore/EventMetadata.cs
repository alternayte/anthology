namespace Anthology.Kernel.EventStore;

public sealed record EventMetadata(
    Guid CorrelationId,
    Guid? CausationId,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    Guid? UserId = null,
    Guid? ContextId = null);
