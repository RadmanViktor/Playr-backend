using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Playr.Api.Models.Auth;
using Playr.Api.Models.Profiles;
using Playr.Application.Email;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;

namespace Playr.IntegrationTests;

public sealed class HttpAuthProfileFlowTests : IClassFixture<PlayrWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PlayrWebApplicationFactory _factory;

    public HttpAuthProfileFlowTests(PlayrWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Auth_and_profile_endpoints_work_through_http_pipeline()
    {
        using var client = _factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "player@example.com",
            "player",
            "Password123"));
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var registered = await registerResponse.Content.ReadFromJsonAsync<UserResponse>();
        registered.Should().NotBeNull();
        registered!.Email.Should().Be("player@example.com");
        registered.Username.Should().Be("player");
        registered.DisplayName.Should().Be("player");
        registered.EmailConfirmed.Should().BeFalse();

        // Login is refused until the emailed link has been followed.
        var unconfirmedLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("player", "Password123"));
        unconfirmedLogin.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await _factory.ConfirmEmailAsync(client, "player@example.com");

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("player", "Password123"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();
        login!.AccessToken.Should().NotBeNullOrWhiteSpace();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var meResponse = await client.GetAsync("/api/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await meResponse.Content.ReadFromJsonAsync<UserResponse>();
        // EmailConfirmed is excluded: it was false at registration and is true now.
        me.Should().BeEquivalentTo(registered, options => options
            .Excluding(user => user.DisplayName)
            .Excluding(user => user.EmailConfirmed));
        me!.DisplayName.Should().Be("player");
        me.EmailConfirmed.Should().BeTrue();

        var publicProfileResponse = await client.GetAsync("/api/profiles/player");
        publicProfileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var publicProfile = await publicProfileResponse.Content.ReadFromJsonAsync<ProfileResponse>(JsonOptions);
        publicProfile.Should().NotBeNull();
        publicProfile!.UserId.Should().Be(registered.Id);
        publicProfile.Username.Should().Be("player");

        var updateResponse = await client.PutAsJsonAsync("/api/profiles/me", new UpdateProfileRequest(
            "Player One",
            "Ready to play",
            "EU",
            ["English"],
            ["PC"],
            new Dictionary<string, string> { ["Steam"] = "https://example.com/player" },
            ["Chess"]));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ProfileResponse>(JsonOptions);
        updated.Should().NotBeNull();
        updated!.DisplayName.Should().Be("Player One");
        updated.Bio.Should().Be("Ready to play");
        updated.Status.Should().Be(ProfileStatus.Online);
    }
}

public sealed class PlayrWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public CapturingEmailSender Emails { get; } = new();

    /// <summary>
    /// Runs the confirmation link that was emailed to <paramref name="email"/>, so the
    /// account can log in. Registration no longer grants access on its own.
    /// </summary>
    public async Task ConfirmEmailAsync(HttpClient client, string email)
    {
        var (userId, token) = Emails.LatestConfirmationFor(email);
        var response = await client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequest(userId, token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:Issuer", "PLAYR_TESTS");
        builder.UseSetting("Jwt:Audience", "PLAYR_TESTS");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-with-at-least-32-chars");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused;Database=unused;Username=unused;Password=unused");
        // These tests exercise the full register -> confirmation-email -> confirm -> login
        // flow. The host environment is Development (see TestHostEnvironment), which by
        // default auto-confirms accounts on register - override that here so the tests keep
        // covering the real production flow instead of skipping it.
        builder.UseSetting("Auth:AutoConfirmEmailOnRegister", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<NpgsqlDataSource>();
            services.RemoveAll<DbContextOptions<PlayrDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PlayrDbContext>>();
            services.AddDbContext<PlayrDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);

            _connection.Open();
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<PlayrDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}

public sealed class CapturingEmailSender : IEmailSender
{
    private readonly List<(string To, string Body)> _sent = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<(string To, string Body)> Sent
    {
        get
        {
            lock (_gate)
            {
                return _sent.ToList();
            }
        }
    }

    public Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _sent.Add((toAddress, htmlBody));
        }

        return Task.CompletedTask;
    }

    public (Guid UserId, string Token) LatestConfirmationFor(string email)
    {
        var body = Sent.LastOrDefault(message => string.Equals(message.To, email, StringComparison.OrdinalIgnoreCase)).Body
            ?? throw new InvalidOperationException($"No confirmation email was sent to {email}.");

        var match = Regex.Match(body, @"confirm-email\?userId=(?<id>[0-9a-fA-F-]+)&amp;token=(?<token>[^""]+)");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find a confirmation link in the email to {email}.");
        }

        return (Guid.Parse(match.Groups["id"].Value), match.Groups["token"].Value);
    }
}
