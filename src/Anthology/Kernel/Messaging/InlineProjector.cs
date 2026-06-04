using System.Data.Common;
using Anthology.Kernel.EventStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Anthology.Kernel.Messaging;

public sealed class InlineProjector(
    InlineProjectionRegistry registry,
    IServiceProvider serviceProvider)
{
    private readonly List<EventEnvelope> _staged = [];

    public void Stage(IReadOnlyList<EventEnvelope> envelopes) => _staged.AddRange(envelopes);

    public async Task ApplyAndSaveAsync(DbTransaction transaction, CancellationToken ct)
    {
        if (_staged.Count == 0) return;

        foreach (var type in registry.ProjectionTypes)
        {
            var projection = (IProjection)serviceProvider.GetRequiredService(type);

            if (projection is IDbContextProjection contextProjection)
                await contextProjection.DbContext.Database.UseTransactionAsync(transaction, ct);

            await projection.ApplyAsync(_staged, ct);

            if (projection is IDbContextProjection dbProjection)
                await dbProjection.DbContext.SaveChangesAsync(ct);
        }

        _staged.Clear();
    }
}

public interface IDbContextProjection
{
    DbContext DbContext { get; }
}
