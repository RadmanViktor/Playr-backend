using FluentAssertions;
using Playr.Application.Auth;

namespace Playr.Application.Tests.Auth;

public sealed class JwtOptionsValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("replace-this-development-key-with-user-secrets-before-production")]
    [InlineData("change-me")]
    [InlineData("short-signing-key")]
    public void ValidateForStartup_WhenSigningKeyIsMissingPlaceholderOrTooShort_Throws(string? signingKey)
    {
        var options = new JwtOptions
        {
            Issuer = "PLAYR",
            Audience = "PLAYR",
            SigningKey = signingKey!,
            ExpirationMinutes = 60
        };

        var act = () => JwtOptionsValidator.ValidateForStartup(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("JWT signing key must be configured with a non-placeholder value of at least 32 characters.");
    }
}
