using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Playr.Application.Auth;
using Playr.Infrastructure;

namespace Playr.IntegrationTests;

public class AuthEndpointConfigurationTests
{
    [Fact]
    public void AddInfrastructure_registers_auth_services()
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

        provider.GetService<JwtTokenGenerator>().Should().NotBeNull();
        provider.GetService<IAuthService>().Should().NotBeNull();
    }

    [Fact]
    public void Auth_api_contract_contains_required_models_and_controller_metadata()
    {
        var apiAssembly = typeof(Program).Assembly;

        var registerRequest = apiAssembly.GetType("Playr.Api.Models.Auth.RegisterRequest");
        registerRequest.Should().NotBeNull();
        AssertRecordParameter<RequiredAttribute>(registerRequest!, "Email");
        AssertRecordParameter<EmailAddressAttribute>(registerRequest!, "Email");
        var usernameLength = GetRecordParameterAttribute<StringLengthAttribute>(registerRequest!, "Username");
        usernameLength.MaximumLength.Should().Be(32);
        usernameLength.MinimumLength.Should().Be(3);
        GetRecordParameterAttribute<MinLengthAttribute>(registerRequest!, "Password").Length.Should().Be(8);

        var loginRequest = apiAssembly.GetType("Playr.Api.Models.Auth.LoginRequest");
        loginRequest.Should().NotBeNull();
        AssertRecordParameter<RequiredAttribute>(loginRequest!, "UsernameOrEmail");
        AssertRecordParameter<RequiredAttribute>(loginRequest!, "Password");

        var loginResponse = apiAssembly.GetType("Playr.Api.Models.Auth.LoginResponse");
        loginResponse.Should().NotBeNull();
        loginResponse!.GetProperty("AccessToken")!.PropertyType.Should().Be(typeof(string));
        loginResponse.GetProperty("ExpiresAt")!.PropertyType.Should().Be(typeof(DateTimeOffset));

        var userResponse = apiAssembly.GetType("Playr.Api.Models.Auth.UserResponse");
        userResponse.Should().NotBeNull();
        userResponse!.GetProperty("Id")!.PropertyType.Should().Be(typeof(Guid));
        userResponse.GetProperty("Email")!.PropertyType.Should().Be(typeof(string));
        userResponse.GetProperty("Username")!.PropertyType.Should().Be(typeof(string));
        userResponse.GetProperty("DisplayName")!.PropertyType.Should().Be(typeof(string));

        var controller = apiAssembly.GetType("Playr.Api.Controllers.AuthController");
        controller.Should().NotBeNull();
        controller!.GetCustomAttribute<ApiControllerAttribute>().Should().NotBeNull();
        controller.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/auth");
        controller.GetMethods()
            .Select(method => method.GetCustomAttribute<HttpPostAttribute>()?.Template)
            .Should().Contain("register");
        controller.GetMethods()
            .Select(method => method.GetCustomAttribute<HttpPostAttribute>()?.Template)
            .Should().Contain("login");
        controller.GetMethods()
            .Where(method => method.GetCustomAttribute<AuthorizeAttribute>() is not null)
            .Select(method => method.GetCustomAttribute<HttpGetAttribute>()?.Template)
            .Should().Contain("me");
    }

    [Fact]
    public void ClaimsPrincipalExtensions_gets_user_id_from_sub_claim()
    {
        var expectedUserId = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", expectedUserId.ToString())]));

        var extensionType = typeof(Program).Assembly.GetType("Playr.Api.Extensions.ClaimsPrincipalExtensions");
        extensionType.Should().NotBeNull();
        var method = extensionType!.GetMethod("GetUserId", BindingFlags.Public | BindingFlags.Static);

        method.Should().NotBeNull();
        method!.Invoke(null, [user]).Should().Be(expectedUserId);
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
