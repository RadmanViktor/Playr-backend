using System.Security.Cryptography;
using System.Text;

namespace Playr.Api.Steam;

/// <summary>
/// Signs and verifies a short opaque "state" value binding a Steam OpenID login flow to the
/// Playr user who initiated it. Steam's callback is an anonymous browser redirect, so we cannot
/// rely on the caller's JWT bearer token at that point - the signed userId in the state parameter
/// (combined with Steam's own OpenID signature verification) is what proves both "this really is
/// Steam confirming a SteamID" and "this callback belongs to this Playr user".
/// </summary>
public sealed class SteamLinkStateSigner(Microsoft.Extensions.Options.IOptions<Playr.Application.Auth.JwtOptions> jwtOptions)
{
    private byte[] Key => Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey);

    public string Sign(Guid userId)
    {
        var payload = userId.ToString("N");
        var signature = Convert.ToHexString(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes(payload)));
        return $"{payload}.{signature}";
    }

    public bool TryVerify(string? state, out Guid userId)
    {
        userId = Guid.Empty;
        if (string.IsNullOrEmpty(state))
        {
            return false;
        }

        var parts = state.Split('.', 2);
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out userId))
        {
            return false;
        }

        var expectedSignature = Convert.ToHexString(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes(parts[0])));
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(parts[1].Length == expectedSignature.Length ? parts[1] : new string('0', expectedSignature.Length)),
            Convert.FromHexString(expectedSignature))
            && string.Equals(parts[1], expectedSignature, StringComparison.OrdinalIgnoreCase);
    }
}
