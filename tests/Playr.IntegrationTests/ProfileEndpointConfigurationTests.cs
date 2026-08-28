using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Playr.Api.Controllers;
using Playr.Api.Models.Profiles;
using Playr.Application.Posts;
using Playr.Application.Profiles;
using Playr.Domain.Profiles;
using Playr.Infrastructure;

namespace Playr.IntegrationTests;

public class ProfileEndpointConfigurationTests
{
    [Fact]
    public void AddInfrastructure_registers_profile_services()
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

        provider.GetService<IProfileService>().Should().NotBeNull();
    }

    [Fact]
    public void Profile_api_contract_contains_required_models_and_controller_metadata()
    {
        var apiAssembly = typeof(Program).Assembly;

        var profileResponse = apiAssembly.GetType("Playr.Api.Models.Profiles.ProfileResponse");
        profileResponse.Should().NotBeNull();
        profileResponse!.GetProperty("UserId")!.PropertyType.Should().Be(typeof(Guid));
        profileResponse.GetProperty("Username")!.PropertyType.Should().Be(typeof(string));
        profileResponse.GetProperty("DisplayName")!.PropertyType.Should().Be(typeof(string));
        profileResponse.GetProperty("Bio")!.PropertyType.Should().Be(typeof(string));
        profileResponse.GetProperty("AvatarUrl")!.PropertyType.Should().Be(typeof(string));
        profileResponse.GetProperty("Region")!.PropertyType.Should().Be(typeof(string));
        profileResponse.GetProperty("Languages")!.PropertyType.Should().Be(typeof(IReadOnlyList<string>));
        profileResponse.GetProperty("Platforms")!.PropertyType.Should().Be(typeof(IReadOnlyList<string>));
        profileResponse.GetProperty("ExternalLinks")!.PropertyType.Should().Be(typeof(IReadOnlyDictionary<string, string>));
        profileResponse.GetProperty("CurrentlyPlayingGames")!.PropertyType.Should().Be(typeof(IReadOnlyList<string>));
        profileResponse.GetProperty("Status")!.PropertyType.Should().Be(typeof(ProfileStatus));
        profileResponse.GetProperty("CreatedAt")!.PropertyType.Should().Be(typeof(DateTimeOffset));
        profileResponse.GetProperty("UpdatedAt")!.PropertyType.Should().Be(typeof(DateTimeOffset));
        profileResponse.GetProperty("LookingForGameNote")!.PropertyType.Should().Be(typeof(string));

        var updateProfileRequest = apiAssembly.GetType("Playr.Api.Models.Profiles.UpdateProfileRequest");
        updateProfileRequest.Should().NotBeNull();
        AssertRecordParameter<RequiredAttribute>(updateProfileRequest!, "DisplayName");
        var displayNameLength = GetRecordParameterAttribute<StringLengthAttribute>(updateProfileRequest!, "DisplayName");
        displayNameLength.MaximumLength.Should().Be(64);
        displayNameLength.MinimumLength.Should().Be(1);
        GetRecordParameterAttribute<StringLengthAttribute>(updateProfileRequest!, "Bio").MaximumLength.Should().Be(500);
        GetRecordParameterAttribute<StringLengthAttribute>(updateProfileRequest!, "Region").MaximumLength.Should().Be(64);

        var controller = apiAssembly.GetType("Playr.Api.Controllers.ProfilesController");
        controller.Should().NotBeNull();
        controller!.GetCustomAttribute<ApiControllerAttribute>().Should().NotBeNull();
        controller.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/profiles");
        controller.GetMethods()
            .Select(method => method.GetCustomAttribute<HttpGetAttribute>()?.Template)
            .Should().Contain("{username}");
        controller.GetMethods()
            .Where(method => method.GetCustomAttribute<AuthorizeAttribute>() is not null)
            .Select(method => method.GetCustomAttribute<HttpPutAttribute>()?.Template)
            .Should().Contain("me");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task UpdateMe_returns_unauthorized_when_user_id_claim_is_missing_or_invalid(string? userIdClaim)
    {
        var controller = new ProfilesController(new ThrowingProfileService(), new ThrowingPostService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateUser(userIdClaim) }
            }
        };

        var result = await controller.UpdateMe(
            new UpdateProfileRequest("Player", null, null, null, null, null, null),
            CancellationToken.None);

        var unauthorized = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.Value.Should().BeEquivalentTo(new { error = "User id claim is missing or invalid." });
    }

    private static void AssertRecordParameter<TAttribute>(Type type, string parameterName)
        where TAttribute : Attribute
    {
        GetRecordParameterAttribute<TAttribute>(type, parameterName).Should().NotBeNull();
    }

    private static TAttribute GetRecordParameterAttribute<TAttribute>(Type type, string parameterName)
        where TAttribute : Attribute
    {
        var parameter = type.GetConstructors().Single().GetParameters().Single(p => p.Name == parameterName);
        return parameter.GetCustomAttribute<TAttribute>()!;
    }

    private static ClaimsPrincipal CreateUser(string? userIdClaim)
    {
        var claims = userIdClaim is null ? [] : new[] { new Claim("sub", userIdClaim) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    private sealed class ThrowingProfileService : IProfileService
    {
        public Task<ProfileDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Profile service should not be called.");

        public Task<ProfileDto?> GetByUsernameAsync(string username, Guid? currentUserId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Profile service should not be called.");

        public Task<ProfileDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Profile service should not be called.");

        public Task<ProfileDto> UpdateCurrentUserAsync(Guid userId, UpdateProfileCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Profile service should not be called.");

        public Task<ProfileDto> UpdateStatusAsync(Guid userId, UpdateStatusCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Profile service should not be called.");

        public Task<ProfileDto> UpdateAvatarAsync(Guid userId, string baseUrl, Playr.Application.Common.FileUploadInput avatar, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Profile service should not be called.");

        public Task<IReadOnlyList<ProfileSearchResult>> SearchAsync(string query, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Profile service should not be called.");

        public Task<IReadOnlyList<LookingForGamePlayerDto>> GetLookingForGamePlayersAsync(Guid currentUserId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Profile service should not be called.");
    }

    private sealed class ThrowingPostService : IPostService
    {
        public Task<PostDto> CreateAsync(Guid authorId, CreatePostCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Post service should not be called.");
        public Task<IReadOnlyList<PostDto>> GetFeedAsync(Guid? currentUserId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Post service should not be called.");
        public Task<PostDto> UpdateAsync(Guid postId, Guid requesterId, UpdatePostCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Post service should not be called.");
        public Task DeleteAsync(Guid postId, Guid requesterId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Post service should not be called.");
        public Task<IReadOnlyList<PostDto>> GetByUsernameAsync(string username, Guid? currentUserId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Post service should not be called.");
        public Task<(int LikesCount, bool Liked)> ToggleLikeAsync(Guid postId, Guid userId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Post service should not be called.");
    }
}
