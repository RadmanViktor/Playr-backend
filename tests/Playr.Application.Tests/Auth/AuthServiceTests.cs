using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Playr.Application.Auth;
using Playr.Application.Email;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Auth;
using Playr.Infrastructure.Data;

namespace Playr.Application.Tests.Auth;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task RegisterAsync_WhenIdentityRejectsPassword_SurfacesIdentityErrorMessage()
    {
        await using var fixture = await AuthFixture.CreateAsync();

        var act = () => fixture.Service.RegisterAsync(
            new RegisterUserCommand("player@example.com", "player", "short"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Passwords must be at least 8 characters*");
    }

    [Fact]
    public async Task RegisterAsync_WhenProfileInsertFails_RemovesIdentityUser()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var existingUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "existing@example.com",
            UserName = "existing"
        };
        (await fixture.UserManager.CreateAsync(existingUser, "Password123")).Succeeded.Should().BeTrue();
        fixture.DbContext.UserProfiles.Add(new UserProfile
        {
            UserId = existingUser.Id,
            Username = "newplayer",
            DisplayName = "Existing"
        });
        await fixture.DbContext.SaveChangesAsync();

        var act = () => fixture.Service.RegisterAsync(
            new RegisterUserCommand("newplayer@example.com", "newplayer", "Password123"),
            CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
        (await fixture.UserManager.FindByNameAsync("newplayer")).Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_SendsConfirmationEmailWithLinkToFrontend()
    {
        await using var fixture = await AuthFixture.CreateAsync();

        var user = await fixture.Service.RegisterAsync(
            new RegisterUserCommand("player@example.com", "player", "Password123"),
            CancellationToken.None);

        user.EmailConfirmed.Should().BeFalse();
        fixture.EmailSender.Sent.Should().ContainSingle();

        var message = fixture.EmailSender.Sent.Single();
        message.To.Should().Be("player@example.com");
        message.Subject.Should().Be(EmailTemplates.ConfirmationSubject);
        message.Body.Should().Contain($"https://playr.test/confirm-email?userId={user.Id}&amp;token=");
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailSendingFails_StillCreatesAccount()
    {
        await using var fixture = await AuthFixture.CreateAsync(configure: sender => sender.ThrowOnSend = true);

        var user = await fixture.Service.RegisterAsync(
            new RegisterUserCommand("player@example.com", "player", "Password123"),
            CancellationToken.None);

        user.Id.Should().NotBeEmpty();
        (await fixture.UserManager.FindByNameAsync("player")).Should().NotBeNull();
    }

    [Fact]
    public async Task ConfirmEmailAsync_WithTokenFromRegistration_ConfirmsTheAccount()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var user = await fixture.Service.RegisterAsync(
            new RegisterUserCommand("player@example.com", "player", "Password123"),
            CancellationToken.None);

        var token = fixture.EmailSender.ExtractToken();

        var confirmed = await fixture.Service.ConfirmEmailAsync(user.Id, token, CancellationToken.None);

        confirmed.Should().BeTrue();
        var stored = await fixture.UserManager.FindByIdAsync(user.Id.ToString());
        stored!.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmEmailAsync_WithGarbageToken_ReturnsFalse()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var user = await fixture.Service.RegisterAsync(
            new RegisterUserCommand("player@example.com", "player", "Password123"),
            CancellationToken.None);

        var confirmed = await fixture.Service.ConfirmEmailAsync(user.Id, "not-a-valid-token", CancellationToken.None);

        confirmed.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmEmailAsync_WithUnknownUser_ReturnsFalse()
    {
        await using var fixture = await AuthFixture.CreateAsync();

        var confirmed = await fixture.Service.ConfirmEmailAsync(Guid.NewGuid(), "token", CancellationToken.None);

        confirmed.Should().BeFalse();
    }

    [Fact]
    public async Task ResendConfirmationAsync_ForUnknownEmail_DoesNothingAndDoesNotThrow()
    {
        await using var fixture = await AuthFixture.CreateAsync();

        await fixture.Service.ResendConfirmationAsync("nobody@example.com", CancellationToken.None);

        fixture.EmailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ResendConfirmationAsync_ForUnconfirmedUser_SendsAnotherEmail()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        await fixture.Service.RegisterAsync(
            new RegisterUserCommand("player@example.com", "player", "Password123"),
            CancellationToken.None);
        fixture.EmailSender.Sent.Clear();

        await fixture.Service.ResendConfirmationAsync("player@example.com", CancellationToken.None);

        fixture.EmailSender.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task ResendConfirmationAsync_ForConfirmedUser_SendsNothing()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        await fixture.CreateConfirmedUserAsync("player", "player@example.com", "Password123");
        fixture.EmailSender.Sent.Clear();

        await fixture.Service.ResendConfirmationAsync("player@example.com", CancellationToken.None);

        fixture.EmailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task LoginAsync_WhenEmailIsNotConfirmed_ThrowsEmailNotConfirmed()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        await fixture.Service.RegisterAsync(
            new RegisterUserCommand("player@example.com", "player", "Password123"),
            CancellationToken.None);

        var act = () => fixture.Service.LoginAsync("player", "Password123", CancellationToken.None);

        await act.Should().ThrowAsync<EmailNotConfirmedException>()
            .WithMessage("Please confirm your email address before logging in.");
    }

    [Fact]
    public async Task LoginAsync_WithWrongPasswordOnUnconfirmedAccount_ReportsGenericFailure()
    {
        // The unconfirmed state must not leak before the password has been verified.
        await using var fixture = await AuthFixture.CreateAsync();
        await fixture.Service.RegisterAsync(
            new RegisterUserCommand("player@example.com", "player", "Password123"),
            CancellationToken.None);

        var act = () => fixture.Service.LoginAsync("player", "wrong-password", CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid username/email or password.");
    }

    [Fact]
    public async Task LoginAsync_AfterConfirmation_ReturnsToken()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var user = await fixture.Service.RegisterAsync(
            new RegisterUserCommand("player@example.com", "player", "Password123"),
            CancellationToken.None);
        await fixture.Service.ConfirmEmailAsync(user.Id, fixture.EmailSender.ExtractToken(), CancellationToken.None);

        var result = await fixture.Service.LoginAsync("player", "Password123", CancellationToken.None);

        result.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsInvalid_IncrementsAccessFailedCount()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var user = await fixture.CreateConfirmedUserAsync("player", "player@example.com", "Password123");

        var act = () => fixture.Service.LoginAsync("player", "wrong-password", CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid username/email or password.");
        (await fixture.UserManager.GetAccessFailedCountAsync(user)).Should().Be(1);
    }

    [Fact]
    public async Task LoginAsync_WhenUserIsLockedOut_ReturnsStablePublicError()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var user = await fixture.CreateConfirmedUserAsync("locked", "locked@example.com", "Password123");
        await fixture.UserManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5));

        var act = () => fixture.Service.LoginAsync("locked", "Password123", CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid username/email or password.");
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsValid_ResetsAccessFailedCount()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var user = await fixture.CreateConfirmedUserAsync("reset", "reset@example.com", "Password123");
        await fixture.UserManager.AccessFailedAsync(user);

        await fixture.Service.LoginAsync("reset", "Password123", CancellationToken.None);

        (await fixture.UserManager.GetAccessFailedCountAsync(user)).Should().Be(0);
    }

    private sealed record SentEmail(string To, string Subject, string Body);

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<SentEmail> Sent { get; } = [];

        public bool ThrowOnSend { get; set; }

        public Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken cancellationToken)
        {
            if (ThrowOnSend)
            {
                throw new InvalidOperationException("SMTP unavailable.");
            }

            Sent.Add(new SentEmail(toAddress, subject, htmlBody));
            return Task.CompletedTask;
        }

        /// <summary>Pulls the url-encoded confirmation token out of the most recent email body.</summary>
        public string ExtractToken()
        {
            var body = Sent.Last().Body;
            var marker = "&amp;token=";
            var start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            var end = body.IndexOf('"', start);
            return body[start..end];
        }
    }

    private sealed class AuthFixture : IAsyncDisposable
    {
        private AuthFixture(SqliteConnection connection, ServiceProvider services, RecordingEmailSender emailSender)
        {
            Connection = connection;
            Services = services;
            EmailSender = emailSender;
            DbContext = services.GetRequiredService<PlayrDbContext>();
            UserManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            Service = services.GetRequiredService<AuthService>();
        }

        public SqliteConnection Connection { get; }
        public ServiceProvider Services { get; }
        public RecordingEmailSender EmailSender { get; }
        public PlayrDbContext DbContext { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        public AuthService Service { get; }

        public async Task<ApplicationUser> CreateConfirmedUserAsync(string username, string email, string password)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                UserName = username,
                EmailConfirmed = true
            };

            (await UserManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
            return user;
        }

        public static async Task<AuthFixture> CreateAsync(Action<RecordingEmailSender>? configure = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var emailSender = new RecordingEmailSender();
            configure?.Invoke(emailSender);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection();
            services.AddDbContext<PlayrDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<ApplicationUser>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                    options.Password.RequiredLength = 8;
                    options.Password.RequireNonAlphanumeric = false;
                    options.SignIn.RequireConfirmedEmail = true;
                    options.Lockout.AllowedForNewUsers = true;
                    options.Lockout.MaxFailedAccessAttempts = 3;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                })
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<PlayrDbContext>()
                .AddDefaultTokenProviders();
            services.AddSingleton(Options.Create(new JwtOptions
            {
                Issuer = "PLAYR",
                Audience = "PLAYR",
                SigningKey = "this-is-a-development-test-key-with-enough-length",
                ExpirationMinutes = 60
            }));
            services.AddSingleton(Options.Create(new FrontendOptions { BaseUrl = "https://playr.test" }));
            services.AddSingleton<IEmailSender>(emailSender);
            services.AddScoped<JwtTokenGenerator>();
            services.AddScoped<AuthService>();

            var provider = services.BuildServiceProvider();
            var fixture = new AuthFixture(connection, provider, emailSender);
            await fixture.DbContext.Database.EnsureCreatedAsync();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
