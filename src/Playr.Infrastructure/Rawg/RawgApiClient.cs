using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Playr.Infrastructure.Rawg;

public sealed record RawgGameSearchResult(long RawgId, string Name, string? CoverImageUrl, string? Genre);

/// <summary>
/// Typed client for the RAWG Video Games Database API (https://api.rawg.io/api).
/// </summary>
public sealed class RawgApiClient(HttpClient httpClient, IOptions<RawgOptions> options, ILogger<RawgApiClient> logger)
{
    private readonly RawgOptions _options = options.Value;

    public async Task<IReadOnlyList<RawgGameSearchResult>> SearchGamesAsync(string query, CancellationToken cancellationToken)
    {
        WarnIfApiKeyMissing();

        var url = $"/api/games?key={_options.ApiKey}&search={Uri.EscapeDataString(query)}&page_size=10";
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "RAWG game search failed for query {Query} with status {StatusCode}.",
                query, (int)response.StatusCode);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<SearchEnvelope>(JsonOptions, cancellationToken);
        var results = payload?.Results;
        if (results is null)
        {
            return [];
        }

        return results.Select(r => new RawgGameSearchResult(
            r.Id,
            r.Name,
            r.BackgroundImage,
            r.Genres is { Count: > 0 } ? string.Join(", ", r.Genres.Select(g => g.Name)) : null)).ToList();
    }

    private bool _hasWarnedAboutApiKey;

    private void WarnIfApiKeyMissing()
    {
        if (_hasWarnedAboutApiKey)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey) || _options.ApiKey == "CHANGE-ME")
        {
            _hasWarnedAboutApiKey = true;
            logger.LogWarning(
                "Rawg:ApiKey is not configured (still set to the default placeholder or empty). " +
                "All RAWG API calls will fail and be treated as 'no results' by callers.");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record SearchEnvelope([property: JsonPropertyName("results")] List<SearchResult>? Results);

    private sealed record SearchResult(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("background_image")] string? BackgroundImage,
        [property: JsonPropertyName("genres")] List<GenreResult>? Genres);

    private sealed record GenreResult([property: JsonPropertyName("name")] string Name);
}
