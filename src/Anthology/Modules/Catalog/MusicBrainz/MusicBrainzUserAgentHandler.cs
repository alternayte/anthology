namespace Anthology.Modules.Catalog;

public sealed class MusicBrainzUserAgentHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.UserAgent.Clear();
        request.Headers.TryAddWithoutValidation("User-Agent", "Anthology/1.0 (https://github.com/AlterNayte/anthology)");
        return base.SendAsync(request, ct);
    }
}
