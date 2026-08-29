namespace Playr.Application.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// SMTP host. When empty, no mail is sent and confirmation links are logged instead.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "PLAYR";

    public bool UseStartTls { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
