using Anthology.Kernel.EventStore;
using Microsoft.AspNetCore.Diagnostics;

namespace Anthology.Kernel;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            ConcurrencyConflict => (409, "Concurrency conflict"),
            _ => (500, "An unexpected error occurred")
        };

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110",
            title,
            status,
            detail = exception.Message
        }, ct);

        return true;
    }
}
