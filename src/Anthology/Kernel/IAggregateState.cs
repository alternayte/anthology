namespace Anthology.Kernel;

public interface IAggregateState<TSelf> where TSelf : IAggregateState<TSelf>
{
    static abstract TSelf Initial { get; }
    static abstract string StreamType { get; }
}
