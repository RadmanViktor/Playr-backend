using System.Net.Http;
using System.Text;

namespace Playr.Infrastructure.Steam;

/// <summary>
/// Implements the relying-party side of Steam's OpenID 2.0 login flow.
/// See https://partner.steamgames.com/doc/features/auth#website
/// </summary>
public sealed class SteamOpenIdService(IHttpClientFactory httpClientFactory)
{
    private const string SteamOpenIdEndpoint = "https://steamcommunity.com/openid/login";
    private const string ClaimedIdPrefix = "https://steamcommunity.com/openid/id/";

    public string BuildRedirectUrl(string returnUrl, string realm)
    {
        var query = new Dictionary<string, string>
        {
            ["openid.ns"] = "http://specs.openid.net/auth/2.0",
            ["openid.mode"] = "checkid_setup",
            ["openid.return_to"] = returnUrl,
            ["openid.realm"] = realm,
            ["openid.identity"] = "http://specs.openid.net/auth/2.0/identifier_select",
            ["openid.claimed_id"] = "http://specs.openid.net/auth/2.0/identifier_select",
        };

        var queryString = string.Join('&', query.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{SteamOpenIdEndpoint}?{queryString}";
    }

    /// <summary>
    /// Verifies the OpenID callback parameters against Steam and, if valid, returns the SteamID64
    /// extracted from the claimed_id. Returns null if the response is invalid.
    /// </summary>
    public async Task<string?> VerifyAndExtractSteamIdAsync(IReadOnlyDictionary<string, string> callbackQuery, CancellationToken cancellationToken)
    {
        if (!callbackQuery.TryGetValue("openid.claimed_id", out var claimedId) ||
            !claimedId.StartsWith(ClaimedIdPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var verificationFields = new Dictionary<string, string>();
        foreach (var (key, value) in callbackQuery)
        {
            if (!key.StartsWith("openid.", StringComparison.Ordinal))
            {
                continue;
            }

            verificationFields[key] = value;
        }

        verificationFields["openid.mode"] = "check_authentication";

        var client = httpClientFactory.CreateClient(nameof(SteamOpenIdService));
        using var content = new FormUrlEncodedContent(verificationFields);
        using var response = await client.PostAsync(SteamOpenIdEndpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!body.Contains("is_valid:true", StringComparison.Ordinal))
        {
            return null;
        }

        var steamId = claimedId[ClaimedIdPrefix.Length..].TrimEnd('/');
        return steamId.Length > 0 && steamId.All(char.IsDigit) ? steamId : null;
    }
}
