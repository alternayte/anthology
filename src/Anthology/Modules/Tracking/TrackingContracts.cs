using Anthology.Kernel;
using Anthology.Kernel.Messaging;

namespace Anthology.Modules.Tracking;

public static class TrackingContracts
{
    public sealed record ItemFinishedIntegrationEvent(Guid StreamId, Guid TitleId, int? Rating, DateTimeOffset At);

    public static void RegisterTranslators(IntegrationEventTranslator translator)
    {
        translator.Register<ItemFinished, ItemFinishedIntegrationEvent>(
            "tracking.item.finished.v1",
            e => new ItemFinishedIntegrationEvent(Guid.Empty, Guid.Empty, e.Rating?.Value, e.At));
    }
}
