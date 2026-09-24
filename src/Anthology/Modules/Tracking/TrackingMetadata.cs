using Deedbox;

namespace Anthology.Modules.Tracking;

/// <summary>Tracking events carry the user and the title in metadata headers, because the events do not hold them.</summary>
public static class TrackingMetadata
{
    private const string UserIdHeader = "userId";
    private const string TitleIdHeader = "titleId";

    public static EventMetadata For(EventMetadata metadata, Guid userId, Guid? titleId = null)
    {
        var headers = new Dictionary<string, string>(metadata.Headers) { [UserIdHeader] = userId.ToString() };
        if (titleId is { } id)
            headers[TitleIdHeader] = id.ToString();
        return metadata with { Actor = $"user:{userId}", Headers = headers };
    }

    public static Guid UserId(EventMetadata metadata) => Required(metadata, UserIdHeader);

    public static Guid TitleId(EventMetadata metadata) => Required(metadata, TitleIdHeader);

    private static Guid Required(EventMetadata metadata, string header) =>
        metadata.Headers.TryGetValue(header, out var value) && Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException($"Tracking event metadata has no '{header}' header.");
}
