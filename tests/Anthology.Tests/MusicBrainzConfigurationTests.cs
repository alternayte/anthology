using System.Net;
using System.Text;
using Anthology.Modules.Catalog;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Anthology.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MusicBrainzConfigurationCollection
{
    public const string Name = "MusicBrainz configuration";
}

[Collection(MusicBrainzConfigurationCollection.Name)]
public class MusicBrainzConfigurationTests
{
    private const string DefaultBaseUrl = "https://musicbrainz.org/ws/2";

    [Fact]
    public async Task MusicBrainz_requests_use_base_url_from_configuration()
    {
        var (services, handler) = BuildProvider(new Dictionary<string, string?>
        {
            ["MusicBrainz:BaseUrl"] = "http://localhost:8080/ws/2"
        });

        using (services)
        {
            await SearchAsync(services);

            handler.RequestUri.Should().Be(
                new Uri("http://localhost:8080/ws/2/release-group?query=test&fmt=json&limit=20"));
        }
    }

    [Fact]
    public async Task MusicBrainz_requests_use_base_url_from_environment_variable()
    {
        var previous = Environment.GetEnvironmentVariable("MusicBrainz__BaseUrl");
        try
        {
            Environment.SetEnvironmentVariable("MusicBrainz__BaseUrl", "http://fake-musicbrainz:8080/ws/2");

            var (services, handler) = BuildProvider(
                new Dictionary<string, string?>(),
                includeEnvironmentVariables: true);

            using (services)
            {
                await SearchAsync(services);

                handler.RequestUri.Should().Be(
                    new Uri("http://fake-musicbrainz:8080/ws/2/release-group?query=test&fmt=json&limit=20"));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("MusicBrainz__BaseUrl", previous);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MusicBrainz_requests_use_default_base_url_when_configuration_is_unset(string? baseUrl)
    {
        var values = new Dictionary<string, string?>();
        if (baseUrl is not null)
            values["MusicBrainz:BaseUrl"] = baseUrl;

        var (services, handler) = BuildProvider(values);

        using (services)
        {
            await SearchAsync(services);

            handler.RequestUri.Should().Be(
                new Uri($"{DefaultBaseUrl}/release-group?query=test&fmt=json&limit=20"));
        }
    }

    private static (ServiceProvider Services, CapturingHandler Handler) BuildProvider(
        Dictionary<string, string?> values,
        bool includeEnvironmentVariables = false)
    {
        var configurationBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Port=5432;Database=anthology_tests;Username=test;Password=test"
            })
            .AddInMemoryCollection(values);

        if (includeEnvironmentVariables)
            configurationBuilder.AddEnvironmentVariables();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCatalogModule(configurationBuilder.Build());

        var handler = new CapturingHandler();
        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler(() => handler));

        return (services.BuildServiceProvider(), handler);
    }

    private static Task SearchAsync(IServiceProvider services) =>
        services.GetRequiredService<IMusicBrainzApi>().SearchReleaseGroupsAsync("test");

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"release-groups\":[]}", Encoding.UTF8, "application/json")
            });
        }
    }
}
