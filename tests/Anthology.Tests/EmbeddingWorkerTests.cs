using System.Net;
using System.Text;
using Anthology.Modules.Catalog;
using Anthology.Workers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Anthology.Tests;

[Collection(EmbeddingEnvironmentCollection.Name)]
public sealed class EmbeddingWorkerTests
{
    [Fact]
    public void BuildEmbeddingText_concatenates_all_fields()
    {
        var text = EmbeddingWorker.BuildEmbeddingText(
            "Interstellar",
            ["Science Fiction", "Drama"],
            ["space travel", "wormhole"],
            "A team of explorers travel through a wormhole."
        );

        text.Should()
            .Be(
                "Interstellar. Science Fiction, Drama. space travel, wormhole. A team of explorers travel through a wormhole."
            );
    }

    [Fact]
    public void BuildEmbeddingText_handles_null_fields()
    {
        var text = EmbeddingWorker.BuildEmbeddingText("Inception", null, null, null);
        text.Should().Be("Inception");
    }

    [Fact]
    public async Task CallEmbeddingApiAsync_posts_to_base_url_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{EmbeddingOptions.Section}:BaseUrl"] = "http://fake:8080/v1",
                }
            )
            .Build();
        var options = configuration.GetSection(EmbeddingOptions.Section).Get<EmbeddingOptions>()!;

        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var worker = CreateWorker(http, options);

        var embedding = await worker.CallEmbeddingApiAsync(
            "Interstellar",
            TestContext.Current.CancellationToken
        );

        embedding.Should().HaveCount(3);
        handler.RequestUri.Should().Be(new Uri("http://fake:8080/v1/embeddings"));
    }

    [Fact]
    public async Task CallEmbeddingApiAsync_defaults_to_openai_endpoint_when_base_url_not_configured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{EmbeddingOptions.Section}:Model"] = "text-embedding-3-small",
                }
            )
            .Build();
        var options = configuration.GetSection(EmbeddingOptions.Section).Get<EmbeddingOptions>()!;

        options.BaseUrl.Should().Be("https://api.openai.com/v1");

        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var worker = CreateWorker(http, options);

        await worker.CallEmbeddingApiAsync("Inception", TestContext.Current.CancellationToken);

        handler.RequestUri.Should().Be(new Uri("https://api.openai.com/v1/embeddings"));
    }

    [Fact]
    public async Task Embedding_BaseUrl_environment_variable_overrides_configuration()
    {
        const string variable = "Embedding__BaseUrl";
        var previous = Environment.GetEnvironmentVariable(variable);

        try
        {
            Environment.SetEnvironmentVariable(variable, "http://fake:8080/v1");

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        [$"{EmbeddingOptions.Section}:BaseUrl"] = "https://api.openai.com/v1",
                    }
                )
                .AddEnvironmentVariables()
                .Build();
            var options = configuration
                .GetSection(EmbeddingOptions.Section)
                .Get<EmbeddingOptions>()!;

            options.BaseUrl.Should().Be("http://fake:8080/v1");

            var handler = new RecordingHandler();
            using var http = new HttpClient(handler);
            var worker = CreateWorker(http, options);

            await worker.CallEmbeddingApiAsync("Inception", TestContext.Current.CancellationToken);

            handler.RequestUri.Should().Be(new Uri("http://fake:8080/v1/embeddings"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void GetEmbeddingsEndpoint_falls_back_to_default_base_url(string? baseUrl)
    {
        EmbeddingWorker
            .GetEmbeddingsEndpoint(baseUrl)
            .Should()
            .Be("https://api.openai.com/v1/embeddings");
    }

    [Theory]
    [InlineData("http://fake:8080/v1", "http://fake:8080/v1/embeddings")]
    [InlineData("http://fake:8080/v1/", "http://fake:8080/v1/embeddings")]
    public void GetEmbeddingsEndpoint_appends_the_embeddings_path(string baseUrl, string expected)
    {
        EmbeddingWorker.GetEmbeddingsEndpoint(baseUrl).Should().Be(expected);
    }

    private static EmbeddingWorker CreateWorker(HttpClient httpClient, EmbeddingOptions options) =>
        new(null!, httpClient, Options.Create(options), NullLogger<EmbeddingWorker>.Instance);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestUri = request.RequestUri;

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"data":[{"embedding":[0.1,0.2,0.3]}]}""",
                        Encoding.UTF8,
                        "application/json"
                    ),
                }
            );
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EmbeddingEnvironmentCollection
{
    public const string Name = "Embedding environment";
}
