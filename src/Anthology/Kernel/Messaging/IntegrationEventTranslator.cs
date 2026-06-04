namespace Anthology.Kernel.Messaging;

public sealed class IntegrationEventTranslator
{
    private readonly Dictionary<Type, Func<IDomainEvent, object?>> _translators = new();

    public void Register<TDomain, TIntegration>(string eventType, Func<TDomain, TIntegration> translate)
        where TDomain : IDomainEvent
        where TIntegration : class
    {
        _translators[typeof(TDomain)] = e => translate((TDomain)e);
    }

    public (string EventType, object Payload)? Translate(IDomainEvent domainEvent)
    {
        if (!_translators.TryGetValue(domainEvent.GetType(), out var translator))
            return null;

        var result = translator(domainEvent);
        return result is null ? null : (domainEvent.GetType().Name, result);
    }
}
