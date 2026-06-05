using Anthology.Kernel.EventStore;

namespace Anthology.Kernel.Messaging;

public sealed class IntegrationEventTranslator
{
    private readonly Dictionary<Type, Func<IDomainEvent, EventEnvelope, object?>> _translators = new();

    public void Register<TDomain, TIntegration>(string eventType, Func<TDomain, EventEnvelope, TIntegration> translate)
        where TDomain : IDomainEvent
        where TIntegration : class
    {
        _translators[typeof(TDomain)] = (e, env) => translate((TDomain)e, env);
    }

    public (string EventType, object Payload)? Translate(IDomainEvent domainEvent, EventEnvelope envelope)
    {
        if (!_translators.TryGetValue(domainEvent.GetType(), out var translator))
            return null;

        var result = translator(domainEvent, envelope);
        return result is null ? null : (domainEvent.GetType().Name, result);
    }
}
