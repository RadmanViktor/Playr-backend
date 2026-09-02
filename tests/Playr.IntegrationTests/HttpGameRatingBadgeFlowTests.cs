using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Playr.Api.Models.Auth;
using Playr.Api.Models.Games;
using Playr.Api.Models.Notifications;

namespace Playr.IntegrationTests;

public sealed class HttpGameRatingBadgeFlowTests : IClassFixture<PlayrWebApplicationFactory>
{
    private static readonly Guid HollowKnightGameId = new("00000001-0000-0000-0000-000000000007");
    private readonly PlayrWebApplicationFactory _factory;

    public HttpGameRatingBadgeFlowTests(PlayrWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Five_star_Hollow_Knight_rating_exposes_badge_details_in_notification_feed()
    {
        using var client = _factory.CreateClient();
        const string email = "voidtouched@example.com";
        const string username = "voidtouched";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, username, "Password123"));
        await _factory.ConfirmEmailAsync(client, email);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(username, "Password123"));
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        (await client.PostAsJsonAsync(
            "/api/profiles/me/library", new AddGameToLibraryRequest(HollowKnightGameId)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync(
            $"/api/profiles/me/library/{HollowKnightGameId}", new RateGameRequest(5, "A masterpiece")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var feed = await client.GetFromJsonAsync<NotificationFeedResponse>("/api/notifications?skip=0&take=20");

        feed.Should().NotBeNull();
        feed!.Items.Should().ContainSingle(notification =>
            notification.Type == "BadgeUnlocked" &&
            notification.BadgeType == "Voidtouched" &&
            notification.BadgeLevel == "Gold" &&
            !notification.IsRead);
    }
}
