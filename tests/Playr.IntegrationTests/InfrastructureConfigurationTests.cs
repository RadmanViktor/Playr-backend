using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Playr.Application.Auth;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;
using Playr.Infrastructure;
using Playr.Infrastructure.Data;

namespace Playr.IntegrationTests;

public class InfrastructureConfigurationTests
{
    [Fact]
    public void PlayrDbContext_configures_user_profile_mapping()
    {
        var options = new DbContextOptionsBuilder<PlayrDbContext>()
            .UseNpgsql("Host=localhost;Database=playr;Username=playr;Password=playr_dev_password")
            .Options;

        using var context = new PlayrDbContext(options);

        var profileType = context.Model.FindEntityType(typeof(UserProfile));
        profileType.Should().NotBeNull();
        profileType!.FindPrimaryKey()!.Properties.Should().ContainSingle(p => p.Name == nameof(UserProfile.UserId));
        profileType.FindIndex(profileType.FindProperty(nameof(UserProfile.Username))!)!.IsUnique.Should().BeTrue();
        profileType.FindProperty(nameof(UserProfile.Username))!.GetMaxLength().Should().Be(32);
        profileType.FindProperty(nameof(UserProfile.DisplayName))!.GetMaxLength().Should().Be(64);
        profileType.FindProperty(nameof(UserProfile.Bio))!.GetMaxLength().Should().Be(500);
        profileType.FindProperty(nameof(UserProfile.AvatarUrl))!.GetMaxLength().Should().Be(500);
        profileType.FindProperty(nameof(UserProfile.Region))!.GetMaxLength().Should().Be(64);
        profileType.FindProperty(nameof(UserProfile.Languages))!.GetColumnType().Should().Be("jsonb");
        profileType.FindProperty(nameof(UserProfile.Platforms))!.GetColumnType().Should().Be("jsonb");
        profileType.FindProperty(nameof(UserProfile.ExternalLinks))!.GetColumnType().Should().Be("jsonb");
        profileType.FindProperty(nameof(UserProfile.CurrentlyPlayingGames))!.GetColumnType().Should().Be("jsonb");

        var foreignKey = profileType.GetForeignKeys().Should().ContainSingle(fk => fk.PrincipalEntityType.ClrType == typeof(ApplicationUser)).Subject;
        foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        foreignKey.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void AddInfrastructure_registers_identity_with_required_options()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=playr;Username=playr;Password=playr_dev_password"
            })
            .Build();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var identityOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>().Value;

        identityOptions.User.RequireUniqueEmail.Should().BeTrue();
        identityOptions.Password.RequiredLength.Should().Be(8);
        identityOptions.Password.RequireNonAlphanumeric.Should().BeFalse();
        provider.GetService<UserManager<ApplicationUser>>().Should().NotBeNull();
        provider.GetService<RoleManager<IdentityRole<Guid>>>().Should().NotBeNull();
        provider.GetService<PlayrDbContext>().Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterAsync_persists_profile_jsonb_defaults_to_postgres()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=playr;Username=playr;Password=playr_dev_password",
                ["Jwt:Issuer"] = "PLAYR",
                ["Jwt:Audience"] = "PLAYR",
                ["Jwt:SigningKey"] = "replace-this-development-key-with-user-secrets-before-production",
                ["Jwt:ExpirationMinutes"] = "60"
            })
            .Build();
        services.AddInfrastructure(configuration);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlayrDbContext>();
        await dbContext.Database.MigrateAsync();
        var uniqueId = Guid.NewGuid().ToString("N");
        var username = $"jsonbUser{uniqueId}"[..32];
        var email = $"jsonb-{uniqueId}@example.com";

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var result = await authService.RegisterAsync(
            new RegisterUserCommand(email, username, "StrongPassword123!"),
            CancellationToken.None);

        var profile = await dbContext.UserProfiles.SingleAsync(p => p.UserId == result.Id, CancellationToken.None);
        profile.ExternalLinks.Should().BeEmpty();
        profile.Languages.Should().BeEmpty();
        profile.Platforms.Should().BeEmpty();
        profile.CurrentlyPlayingGames.Should().BeEmpty();
    }
}
