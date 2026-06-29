using System.ComponentModel.DataAnnotations;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Playr.Application.Profiles;
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
        profileResponse.GetProperty("LookingForPlayers")!.PropertyType.Should().Be(typeof(bool));
        profileResponse.GetProperty("CreatedAt")!.PropertyType.Should().Be(typeof(DateTimeOffset));
        profileResponse.GetProperty("UpdatedAt")!.PropertyType.Should().Be(typeof(DateTimeOffset));

        var updateProfileRequest = apiAssembly.GetType("Playr.Api.Models.Profiles.UpdateProfileRequest");
        updateProfileRequest.Should().NotBeNull();
        AssertRecordParameter<RequiredAttribute>(updateProfileRequest!, "DisplayName");
        var displayNameLength = GetRecordParameterAttribute<StringLengthAttribute>(updateProfileRequest!, "DisplayName");
        displayNameLength.MaximumLength.Should().Be(64);
        displayNameLength.MinimumLength.Should().Be(1);
        GetRecordParameterAttribute<StringLengthAttribute>(updateProfileRequest!, "Bio").MaximumLength.Should().Be(500);
        GetRecordParameterAttribute<StringLengthAttribute>(updateProfileRequest!, "AvatarUrl").MaximumLength.Should().Be(500);
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
}
