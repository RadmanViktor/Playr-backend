namespace Playr.Infrastructure.Rawg;

public sealed class RawgOptions
{
    public const string SectionName = "Rawg";

    /// <summary>
    /// RAWG.io API key, obtained from https://rawg.io/apidocs.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
