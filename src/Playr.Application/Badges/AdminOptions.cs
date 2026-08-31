namespace Playr.Application.Badges;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>
    /// Shared secret required (via the <c>X-Admin-Secret</c> header) to call
    /// admin-only endpoints such as manually granting a badge. Leave empty to
    /// disable these endpoints entirely (the default, safe for local dev without
    /// configuring anything and for any environment where the secret isn't set).
    /// </summary>
    public string GrantSecret { get; set; } = string.Empty;
}
