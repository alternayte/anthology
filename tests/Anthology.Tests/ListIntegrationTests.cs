using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Anthology.Modules.Catalog;
using Anthology.Modules.Tracking;
using Anthology.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Anthology.Tests;

public sealed class ListIntegrationTests(WebAppFixture fixture)
    : IClassFixture<WebAppFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = fixture.Factory.CreateClient();
        var email = $"test-{Guid.NewGuid():N}@example.com";
        var password = "TestPassword123";
        await client.PostAsJsonAsync("/api/identity/register",
            new { Email = email, Password = password }, TestContext.Current.CancellationToken);
        await client.PostAsJsonAsync("/api/identity/login",
            new { Email = email, Password = password }, TestContext.Current.CancellationToken);
        return client;
    }

    private async Task<Guid> SeedTitleAsync(string name = "The Matrix")
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var title = new Title
        {
            TitleId = Guid.NewGuid(),
            ExternalId = $"tmdb-{Guid.NewGuid():N}",
            MediaType = MediaType.Film,
            Name = name,
            Year = 1999, PosterPath = "/poster.jpg", Overview = "A test film."
        };
        catalogDb.Titles.Add(title);
        await catalogDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        return title.TitleId;
    }

    [Fact]
    public async Task Create_list_returns_ok_and_persists()
    {
        var client = await CreateAuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/tracking/lists",
            new { Name = "Favourites", Description = "My favourite films", Visibility = "Private" },
            TestContext.Current.CancellationToken);

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var created = await createResponse.Content.ReadFromJsonAsync<CuratedListDto>(JsonOptions,
            TestContext.Current.CancellationToken);
        created.Should().NotBeNull();
        created!.Name.Should().Be("Favourites");
        created.Description.Should().Be("My favourite films");
        created.ItemCount.Should().Be(0);

        var listsResponse = await client.GetAsync("/api/tracking/lists",
            TestContext.Current.CancellationToken);
        listsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var lists = await listsResponse.Content.ReadFromJsonAsync<List<GetUserLists.ListSummaryDto>>(
            JsonOptions, TestContext.Current.CancellationToken);
        lists.Should().Contain(l => l.ListId == created.ListId && l.Name == "Favourites");
    }

    [Fact]
    public async Task Add_items_and_get_list_returns_ordered_items()
    {
        var client = await CreateAuthenticatedClientAsync();
        var titleId1 = await SeedTitleAsync("Inception");
        var titleId2 = await SeedTitleAsync("Interstellar");

        var createResponse = await client.PostAsJsonAsync("/api/tracking/lists",
            new { Name = "Nolan Films", Description = (string?)null, Visibility = "Private" },
            TestContext.Current.CancellationToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<CuratedListDto>(JsonOptions,
            TestContext.Current.CancellationToken);

        await client.PostAsJsonAsync($"/api/tracking/lists/{created!.ListId}/items",
            new { TitleId = titleId1 }, TestContext.Current.CancellationToken);
        await client.PostAsJsonAsync($"/api/tracking/lists/{created.ListId}/items",
            new { TitleId = titleId2 }, TestContext.Current.CancellationToken);

        var detailResponse = await client.GetAsync($"/api/tracking/lists/{created.ListId}",
            TestContext.Current.CancellationToken);
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailResponse.Content.ReadFromJsonAsync<GetList.ListDetailDto>(JsonOptions,
            TestContext.Current.CancellationToken);
        detail.Should().NotBeNull();
        detail!.Items.Should().HaveCount(2);
        detail.Items[0].Position.Should().BeLessThan(detail.Items[1].Position);
        detail.Items.Select(i => i.Title).Should().Contain("Inception").And.Contain("Interstellar");
    }

    [Fact]
    public async Task Delete_list_hides_from_user_lists()
    {
        var client = await CreateAuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/tracking/lists",
            new { Name = "To Delete", Description = (string?)null, Visibility = "Private" },
            TestContext.Current.CancellationToken);
        var created = await createResponse.Content.ReadFromJsonAsync<CuratedListDto>(JsonOptions,
            TestContext.Current.CancellationToken);

        var deleteResponse = await client.DeleteAsync($"/api/tracking/lists/{created!.ListId}",
            TestContext.Current.CancellationToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listsResponse = await client.GetAsync("/api/tracking/lists",
            TestContext.Current.CancellationToken);
        var lists = await listsResponse.Content.ReadFromJsonAsync<List<GetUserLists.ListSummaryDto>>(
            JsonOptions, TestContext.Current.CancellationToken);
        lists.Should().NotContain(l => l.ListId == created.ListId);
    }

    [Fact]
    public async Task Private_list_not_visible_to_other_users()
    {
        var clientA = await CreateAuthenticatedClientAsync();
        var clientB = await CreateAuthenticatedClientAsync();

        var createResponse = await clientA.PostAsJsonAsync("/api/tracking/lists",
            new { Name = "Private List", Description = (string?)null, Visibility = "Private" },
            TestContext.Current.CancellationToken);
        var created = await createResponse.Content.ReadFromJsonAsync<CuratedListDto>(JsonOptions,
            TestContext.Current.CancellationToken);

        var response = await clientB.GetAsync($"/api/tracking/lists/{created!.ListId}",
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Public_by_link_list_visible_to_other_users()
    {
        var clientA = await CreateAuthenticatedClientAsync();
        var clientB = await CreateAuthenticatedClientAsync();

        var createResponse = await clientA.PostAsJsonAsync("/api/tracking/lists",
            new { Name = "Shared List", Description = "Visible to anyone with link", Visibility = "PublicByLink" },
            TestContext.Current.CancellationToken);
        var created = await createResponse.Content.ReadFromJsonAsync<CuratedListDto>(JsonOptions,
            TestContext.Current.CancellationToken);

        var response = await clientB.GetAsync($"/api/tracking/lists/{created!.ListId}",
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await response.Content.ReadFromJsonAsync<GetList.ListDetailDto>(JsonOptions,
            TestContext.Current.CancellationToken);
        detail.Should().NotBeNull();
        detail!.Name.Should().Be("Shared List");
    }

    [Fact]
    public async Task Unauthenticated_list_creation_returns_401()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/tracking/lists",
            new { Name = "Should Fail", Description = (string?)null, Visibility = "Private" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
