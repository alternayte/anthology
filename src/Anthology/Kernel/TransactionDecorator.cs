using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Anthology.Kernel;

public sealed class TransactionDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    EventStoreDbContext db,
    InlineProjector projector,
    OutboxWriter outbox,
    NpgsqlConnection connection)
    : ICommandHandler<TCommand, TResult>
    where TResult : IResultUnion<TResult>
{
    public async Task<TResult> Handle(TCommand command, CancellationToken ct)
    {
        if (command is not IEventSourcedCommand)
            return await inner.Handle(command, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var result = await inner.Handle(command, ct);
        if (result.IsError)
        {
            await tx.RollbackAsync(ct);
            return result;
        }

        await projector.ApplyAndSaveAsync(tx.GetDbTransaction(), ct);
        await outbox.WriteStagedAsync(ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "NOTIFY new_events";
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        return result;
    }
}
