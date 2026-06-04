using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;

namespace Anthology.Kernel;

public sealed class TransactionDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    EventStoreDbContext db,
    InlineProjector projector,
    OutboxWriter outbox)
    : ICommandHandler<TCommand, TResult>
    where TResult : IResultUnion<TResult>
{
    public async Task<TResult> Handle(TCommand command, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var result = await inner.Handle(command, ct);

        if (result.IsError)
        {
            await tx.RollbackAsync(ct);
            return result;
        }

        await projector.ApplyStagedAsync(ct);
        await outbox.WriteStagedAsync(ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return result;
    }
}
