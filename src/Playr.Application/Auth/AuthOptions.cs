namespace Playr.Application.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// When true, newly registered accounts are marked as email-confirmed immediately,
    /// skipping the confirmation email step. Intended for local development only.
    /// </summary>
    public bool AutoConfirmEmailOnRegister { get; set; } = false;

    public int RefreshTokenExpirationDays { get; set; } = 30;
    public int RefreshTokenAbsoluteExpirationDays { get; set; } = 90;
}
