namespace Playr.Application.Auth;

public static class JwtOptionsValidator
{
    private const int MinimumSigningKeyLength = 32;

    public static void ValidateForStartup(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            throw new InvalidOperationException("JWT issuer must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException("JWT audience must be configured.");
        }

        if (IsInvalidSigningKey(options.SigningKey))
        {
            throw new InvalidOperationException("JWT signing key must be configured with a non-placeholder value of at least 32 characters.");
        }
    }

    private static bool IsInvalidSigningKey(string? signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < MinimumSigningKeyLength)
        {
            return true;
        }

        var normalized = signingKey.Trim().ToUpperInvariant();
        return normalized.Contains("REPLACE") || normalized.Contains("CHANGE-ME") || normalized.Contains("PLACEHOLDER");
    }
}
