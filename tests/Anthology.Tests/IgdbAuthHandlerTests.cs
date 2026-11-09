using System.Net;
using System.Net.Sockets;
using System.Text;
using Anthology.Modules.Catalog;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Anthology.Tests;

public class IgdbAuthHandlerTests
{
    [Fact]
    public void IgdbOptions_defaults_TokenUrl_to_the_twitch_token_endpoint()
    {
        new IgdbOptions().TokenUrl.Should().Be("https://id.twitch.tv/oauth2/token");
    }

    [Fact]
    public void IgdbOptions_binds_TokenUrl_from_configuration_key()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Igdb:TokenUrl"] = "http://localhost:5055/oauth2/token"
            })
            .Build();

        BindOptions(configuration).TokenUrl.Should().Be("http://localhost:5055/oauth2/token");
    }

    [Fact]
    public void IgdbOptions_binds_TokenUrl_from_environment_variable()
    {
        const string name = "Igdb__TokenUrl";
        var previous = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, "http://localhost:5056/env-token");

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            BindOptions(configuration).TokenUrl.Should().Be("http://localhost:5056/env-token");
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, previous);
        }
    }

    [Fact]
    public async Task IgdbAuthHandler_requests_token_from_configured_url_and_uses_access_token_as_bearer()
    {
        await using var server = new FakeTokenServer(
            """{"access_token":"fake-access-token","expires_in":3600}""");

        var options = Options.Create(new IgdbOptions
        {
            ClientId = "test-client",
            ClientSecret = "test-secret",
            TokenUrl = server.TokenUrl
        });

        var terminal = new CapturingHandler();
        using var handler = new IgdbAuthHandler(options) { InnerHandler = terminal };
        using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.test/v4/games");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await server.Received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        server.Method.Should().Be("POST");
        var tokenUri = new Uri("http://localhost" + server.Target);
        tokenUri.AbsolutePath.Should().Be("/fake/token");

        var query = ParseQuery(tokenUri.Query);
        query.Should().Contain("client_id", "test-client");
        query.Should().Contain("client_secret", "test-secret");
        query.Should().Contain("grant_type", "client_credentials");

        terminal.AuthorizationScheme.Should().Be("Bearer");
        terminal.AuthorizationParameter.Should().Be("fake-access-token");
        terminal.ClientId.Should().Be("test-client");
    }

    private static IgdbOptions BindOptions(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.Configure<IgdbOptions>(configuration.GetSection(IgdbOptions.Section));
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<IgdbOptions>>().Value;
    }

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty);

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string? ClientId { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            ClientId = request.Headers.TryGetValues("Client-ID", out var values) ? values.Single() : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class FakeTokenServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _loop;

        public FakeTokenServer(string responseJson)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            TokenUrl = $"http://127.0.0.1:{port}/fake/token";
            _loop = Task.Run(() => ServeOnceAsync(responseJson));
        }

        public string TokenUrl { get; }
        public string? Method { get; private set; }
        public string? Target { get; private set; }
        public TaskCompletionSource Received { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private async Task ServeOnceAsync(string responseJson)
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync();
                var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

                var requestLine = (await reader.ReadLineAsync())?.Split(' ');
                Method = requestLine?[0];
                Target = requestLine?[1];

                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(line["Content-Length:".Length..].Trim(), out var length) &&
                        length > 0)
                    {
                        var buffer = new char[length];
                        await reader.ReadBlockAsync(buffer.AsMemory());
                    }
                }

                var body = Encoding.UTF8.GetBytes(responseJson);
                var headers = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(headers);
                await stream.WriteAsync(body);
                await stream.FlushAsync();
                Received.TrySetResult();
            }
            catch (Exception ex)
            {
                Received.TrySetException(ex);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            try
            {
                await _loop.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
            }
        }
    }
}
