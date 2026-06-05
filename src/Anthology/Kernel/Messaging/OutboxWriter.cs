using System.Text.Json;
using Anthology.Kernel.EventStore;

namespace Anthology.Kernel.Messaging;

public sealed class OutboxWriter(EventStoreDbContext db, IntegrationEventTranslator translator)
{
    private readonly List<EventEnvelope> _staged = [];

    public void Stage(IReadOnlyList<EventEnvelope> envelopes) => _staged.AddRange(envelopes);

    public Task WriteStagedAsync(CancellationToken ct)
    {
        foreach (var envelope in _staged)
        {
            var translated = translator.Translate(envelope.Event, envelope);
            if (translated is null) continue;

            var (eventType, payload) = translated.Value;
            db.Outbox.Add(new OutboxRow
            {
                Id = Guid.NewGuid(),
                AggregateType = "TrackedItem",
                AggregateId = envelope.StreamId.ToString(),
                Type = eventType,
                Payload = JsonSerializer.Serialize(payload, EventSerializer.Options),
                OccurredAt = envelope.Metadata.OccurredAt
            });
        }

        _staged.Clear();
        return Task.CompletedTask;
    }
}
