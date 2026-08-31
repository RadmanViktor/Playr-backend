using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Playr.Application.Auth;
using Playr.Application.Profiles;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;

namespace Playr.Application.Tests;

public class ContractTests
{
    [Fact]
    public void ApplicationUser_uses_identity_guid_user_and_initializes_created_at()
    {
        var before = DateTimeOffset.UtcNow;

        var user = new ApplicationUser();

        user.Should().BeAssignableTo<IdentityUser<Guid>>();
        user.CreatedAt.Should().BeOnOrAfter(before);
        user.CreatedAt.Should().BeOnOrBefore(DateTimeOffset.UtcNow);
        user.Profile.Should().BeNull();
    }

    [Fact]
    public void UserProfile_initializes_required_defaults_and_collections()
    {
        var before = DateTimeOffset.UtcNow;

        var profile = new UserProfile();

        profile.Username.Should().BeEmpty();
        profile.DisplayName.Should().BeEmpty();
        profile.Bio.Should().BeNull();
        profile.AvatarUrl.Should().BeNull();
        profile.Region.Should().BeNull();
        profile.Languages.Should().BeEmpty();
        profile.Platforms.Should().BeEmpty();
        profile.ExternalLinks.Should().BeEmpty();
        profile.Status.Should().Be(ProfileStatus.Online);
        profile.LookingForGameId.Should().BeNull();
        profile.LookingForPlayStyle.Should().BeNull();
        profile.CreatedAt.Should().BeOnOrAfter(before);
        profile.CreatedAt.Should().BeOnOrBefore(DateTimeOffset.UtcNow);
        profile.UpdatedAt.Should().BeOnOrAfter(before);
        profile.UpdatedAt.Should().BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Auth_contracts_expose_required_shapes()
    {
        var command = new RegisterUserCommand("player@example.com", "player", "password");
        var authUser = new AuthUserDto(Guid.NewGuid(), "player@example.com", "player", "Player", false);
        var result = new AuthResult("token", DateTimeOffset.UtcNow.AddHours(1));
        var options = new JwtOptions();

        command.Email.Should().Be("player@example.com");
        command.Username.Should().Be("player");
        command.Password.Should().Be("password");
        authUser.Email.Should().Be("player@example.com");
        result.AccessToken.Should().Be("token");
        JwtOptions.SectionName.Should().Be("Jwt");
        options.Issuer.Should().BeEmpty();
        options.Audience.Should().BeEmpty();
        options.SigningKey.Should().BeEmpty();
        options.ExpirationMinutes.Should().Be(60);
        typeof(IAuthService).GetMethods().Select(method => method.Name).Should().BeEquivalentTo(
            "RegisterAsync",
            "LoginAsync",
            "GetCurrentUserAsync",
            "ConfirmEmailAsync",
            "ResendConfirmationAsync");
    }

    [Fact]
    public void Profile_contracts_expose_required_shapes()
    {
        string[] languages = ["en"];
        string[] platforms = ["pc"];
        string[] genres = ["FPS"];
        Dictionary<string, string> externalLinks = new() { ["twitch"] = "https://example.com" };
        var createdAt = DateTimeOffset.UtcNow;
        var updatedAt = createdAt.AddMinutes(1);

        var profile = new ProfileDto(
            Guid.NewGuid(),
            "player",
            "Player",
            "bio",
            "avatar",
            "cover",
            "region",
            languages,
            platforms,
            genres,
            externalLinks,
            ProfileStatus.LookingForGame,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            false,
            createdAt,
            updatedAt);
        var command = new UpdateProfileCommand(
            "Player",
            "bio",
            "region",
            languages,
            platforms,
            genres,
            externalLinks);

        profile.Username.Should().Be("player");
        profile.Languages.Should().BeEquivalentTo(languages);
        profile.ExternalLinks.Should().ContainKey("twitch");
        profile.Status.Should().Be(ProfileStatus.LookingForGame);
        profile.UpdatedAt.Should().Be(updatedAt);
        command.DisplayName.Should().Be("Player");
        command.Genres.Should().BeEquivalentTo(genres);
        typeof(IProfileService).GetMethods().Select(method => method.Name).Should().BeEquivalentTo(
            "GetByUsernameAsync",
            "GetByUserIdAsync",
            "UpdateCurrentUserAsync",
            "UpdateStatusAsync",
            "UpdateAvatarAsync",
            "UpdateCoverImageAsync",
            "SetOfflineAsync",
            "SetOnlineIfOfflineAsync",
            "SearchAsync",
            "GetLookingForGamePlayersAsync");
    }
}
