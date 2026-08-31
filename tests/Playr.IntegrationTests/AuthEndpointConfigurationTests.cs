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
        services.AddScoped<Playr.Application.Notifications.INotificationNotifier, NoOpNotificationNotifier>();

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
        userResponse.GetProperty("EmailConfirmed")!.PropertyType.Should().Be(typeof(bool));

        var confirmEmailRequest = apiAssembly.GetType("Playr.Api.Models.Auth.ConfirmEmailRequest");
        confirmEmailRequest.Should().NotBeNull();
        AssertRecordParameter<RequiredAttribute>(confirmEmailRequest!, "UserId");
        AssertRecordParameter<RequiredAttribute>(confirmEmailRequest!, "Token");

        var resendRequest = apiAssembly.GetType("Playr.Api.Models.Auth.ResendConfirmationRequest");
        resendRequest.Should().NotBeNull();
        AssertRecordParameter<RequiredAttribute>(resendRequest!, "Email");
        AssertRecordParameter<EmailAddressAttribute>(resendRequest!, "Email");

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
            .Select(method => method.GetCustomAttribute<HttpPostAttribute>()?.Template)
            .Should().Contain("confirm-email");
        controller.GetMethods()
            .Select(method => method.GetCustomAttribute<HttpPostAttribute>()?.Template)
            .Should().Contain("resend-confirmation");
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

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task Me_returns_unauthorized_when_user_id_claim_is_missing_or_invalid(string? userIdClaim)
    {
        var controller = new AuthController(new ThrowingAuthService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateUser(userIdClaim) }
            }
        };

        var result = await controller.Me(CancellationToken.None);

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

    private sealed class ThrowingAuthService : IAuthService
    {
        public Task<AuthUserDto> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Auth service should not be called.");

        public Task<AuthResult> LoginAsync(string usernameOrEmail, string password, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Auth service should not be called.");

        public Task<AuthUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Auth service should not be called.");

        public Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Auth service should not be called.");

        public Task ResendConfirmationAsync(string email, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Auth service should not be called.");
    }

    private sealed class NoOpNotificationNotifier : Playr.Application.Notifications.INotificationNotifier
    {
        public Task NotifyNotificationCreatedAsync(Playr.Application.Notifications.NotificationDto notification, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
