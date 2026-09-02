using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Controllers;
using Playr.Api.Models.Profiles;

namespace Playr.IntegrationTests;

public sealed class PublicLookingForGameEndpointConfigurationTests
{
    [Fact]
    public void Public_lobby_has_a_restricted_anonymous_contract()
    {
        typeof(PublicLookingForGameSummaryResponse).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["TotalCount", "FeaturedGame", "Players"]);
        typeof(PublicLookingForGameFeaturedGameResponse).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["Name", "CoverImageUrl", "PlayerCount"]);
        typeof(PublicLookingForGamePlayerResponse).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["Username", "DisplayName", "AvatarUrl", "GameName", "PlayStyle"]);

        var controller = typeof(PublicLookingForGameController);
        controller.GetCustomAttribute<ApiControllerAttribute>().Should().NotBeNull();
        controller.GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/profiles/looking-for-game/public");

        var method = controller.GetMethod(nameof(PublicLookingForGameController.Get));
        method.Should().NotBeNull();
        method!.GetCustomAttribute<HttpGetAttribute>().Should().NotBeNull();
        method.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
        method.GetCustomAttribute<AuthorizeAttribute>().Should().BeNull();
    }
}
