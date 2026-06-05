namespace Anthology.Kernel;

public interface ICommand<TResult>;

public interface IEventSourcedCommand
{
    Guid UserId { get; }
    Guid StreamId { get; }
    DateTimeOffset At { get; }
    (Guid? UserId, Guid? ContextId) GetCorrelationHints();
}
