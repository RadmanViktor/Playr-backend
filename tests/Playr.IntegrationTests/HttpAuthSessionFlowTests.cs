using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Playr.Api.Models.Auth;
using Playr.Infrastructure.Data;

namespace Playr.IntegrationTests;

public sealed class HttpAuthSessionFlowTests : IClassFixture<PlayrWebApplicationFactory>
{
    private const string RefreshCookieName = "playr_refresh";
    private readonly PlayrWebApplicationFactory _factory;

    public HttpAuthSessionFlowTests(PlayrWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_sets_a_protected_refresh_cookie()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });
        var loginResponse = await RegisterConfirmAndLoginAsync(client, "session-cookie");

        var setCookie = GetRefreshSetCookie(loginResponse);

        setCookie.Should().Contain("httponly");
        setCookie.Should().Contain("secure");
        setCookie.Should().Contain("samesite=lax");
        setCookie.Should().Contain("path=/api/auth");
    }

    [Fact]
    public async Task Refresh_rotates_the_cookie_and_reuse_revokes_the_token_family()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var loginResponse = await RegisterConfirmAndLoginAsync(client, "session-rotate");
        var originalCookie = GetCookiePair(loginResponse);

        var refreshResponse = await PostSessionAsync(client, "/api/auth/refresh", originalCookie);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>();
        refreshed!.AccessToken.Should().NotBeNullOrWhiteSpace();
        var rotatedCookie = GetCookiePair(refreshResponse);
        rotatedCookie.Should().NotBe(originalCookie);

        var replayResponse = await PostSessionAsync(client, "/api/auth/refresh", originalCookie);
        replayResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var familyResponse = await PostSessionAsync(client, "/api/auth/refresh", rotatedCookie);
        familyResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token_and_clears_the_cookie()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var loginResponse = await RegisterConfirmAndLoginAsync(client, "session-logout");
        var cookie = GetCookiePair(loginResponse);

        var logoutResponse = await PostSessionAsync(client, "/api/auth/logout", cookie);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        GetRefreshSetCookie(logoutResponse).Should().Contain("expires=thu, 01 jan 1970");

        var refreshResponse = await PostSessionAsync(client, "/api/auth/refresh", cookie);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_requires_the_csrf_header()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var loginResponse = await RegisterConfirmAndLoginAsync(client, "session-csrf");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", GetCookiePair(loginResponse));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Expired_refresh_token_is_rejected()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var loginResponse = await RegisterConfirmAndLoginAsync(client, "session-expired");
        var cookie = GetCookiePair(loginResponse);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlayrDbContext>();
            await dbContext.RefreshSessions
                .Where(session => session.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    session => session.ExpiresAt,
                    DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        var response = await PostSessionAsync(client, "/api/auth/refresh", cookie);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> RegisterConfirmAndLoginAsync(HttpClient client, string username)
    {
        var email = $"{username}@example.com";
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, username, "Password123"));
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.ConfirmEmailAsync(client, email);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(username, "Password123"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return loginResponse;
    }

    private static async Task<HttpResponseMessage> PostSessionAsync(HttpClient client, string path, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Cookie", cookie);
        request.Headers.Add("X-Playr-Session", "1");
        return await client.SendAsync(request);
    }

    private static string GetCookiePair(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith($"{RefreshCookieName}=", StringComparison.OrdinalIgnoreCase));
        return setCookie.Split(';', 2)[0];
    }

    private static string GetRefreshSetCookie(HttpResponseMessage response)
    {
        return response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith($"{RefreshCookieName}=", StringComparison.OrdinalIgnoreCase))
            .ToLowerInvariant();
    }
}
