using FluentAssertions;
using Microsoft.Extensions.Options;
using Playr.Application.Auth;
using Playr.Domain.Identity;

namespace Playr.Application.Tests.Auth;

public sealed class JwtTokenGeneratorTests
{
    [Fact]
    public void Generate_ReturnsTokenAndExpiration()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "PLAYR",
            Audience = "PLAYR",
            SigningKey = "this-is-a-development-test-key-with-enough-length",
            ExpirationMinutes = 15
        });
        var generator = new JwtTokenGenerator(options);
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            UserName = "playerOne"
        };

        var result = generator.Generate(user);

        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(10));
    }
}
