using Microsoft.Extensions.Options;

namespace Anthology.Modules.Catalog;

public sealed class TmdbAuthHandler(IOptions<TmdbOptions> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var uri = request.RequestUri!;
        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        request.RequestUri = new Uri($"{uri}{separator}api_key={options.Value.ApiKey}");
        return base.SendAsync(request, ct);
    }
}
