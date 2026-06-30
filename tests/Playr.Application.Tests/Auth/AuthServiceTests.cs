using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Playr.Application.Auth;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Auth;
using Playr.Infrastructure.Data;

namespace Playr.Application.Tests.Auth;

public sealed class AuthServiceTests
{
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
    public async Task LoginAsync_WhenPasswordIsInvalid_IncrementsAccessFailedCount()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "player@example.com",
            UserName = "player"
        };
        (await fixture.UserManager.CreateAsync(user, "Password123")).Succeeded.Should().BeTrue();

        var act = () => fixture.Service.LoginAsync("player", "wrong-password", CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await fixture.UserManager.GetAccessFailedCountAsync(user)).Should().Be(1);
    }

    [Fact]
    public async Task LoginAsync_WhenUserIsLockedOut_DoesNotIssueToken()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "locked@example.com",
            UserName = "locked"
        };
        (await fixture.UserManager.CreateAsync(user, "Password123")).Succeeded.Should().BeTrue();
        await fixture.UserManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5));

        var act = () => fixture.Service.LoginAsync("locked", "Password123", CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User account is locked out.");
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsValid_ResetsAccessFailedCount()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "reset@example.com",
            UserName = "reset"
        };
        (await fixture.UserManager.CreateAsync(user, "Password123")).Succeeded.Should().BeTrue();
        await fixture.UserManager.AccessFailedAsync(user);

        await fixture.Service.LoginAsync("reset", "Password123", CancellationToken.None);

        (await fixture.UserManager.GetAccessFailedCountAsync(user)).Should().Be(0);
    }

    private sealed class AuthFixture : IAsyncDisposable
    {
        private AuthFixture(SqliteConnection connection, ServiceProvider services)
        {
            Connection = connection;
            Services = services;
            DbContext = services.GetRequiredService<PlayrDbContext>();
            UserManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            Service = services.GetRequiredService<AuthService>();
        }

        public SqliteConnection Connection { get; }
        public ServiceProvider Services { get; }
        public PlayrDbContext DbContext { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        public AuthService Service { get; }

        public static async Task<AuthFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<PlayrDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<ApplicationUser>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                    options.Password.RequiredLength = 8;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Lockout.AllowedForNewUsers = true;
                    options.Lockout.MaxFailedAccessAttempts = 3;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                })
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<PlayrDbContext>();
            services.AddSingleton(Options.Create(new JwtOptions
            {
                Issuer = "PLAYR",
                Audience = "PLAYR",
                SigningKey = "this-is-a-development-test-key-with-enough-length",
                ExpirationMinutes = 60
            }));
            services.AddScoped<JwtTokenGenerator>();
            services.AddScoped<AuthService>();

            var provider = services.BuildServiceProvider();
            var fixture = new AuthFixture(connection, provider);
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
