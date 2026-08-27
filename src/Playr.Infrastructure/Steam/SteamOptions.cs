namespace Playr.Infrastructure.Steam;

public sealed class SteamOptions
{
    public const string SectionName = "Steam";

    /// <summary>
    /// Steam Web API key, obtained from https://steamcommunity.com/dev/apikey.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// How often the background sync job re-fetches each linked user's game library.
    /// </summary>
    public int SyncIntervalHours { get; set; } = 12;
}
