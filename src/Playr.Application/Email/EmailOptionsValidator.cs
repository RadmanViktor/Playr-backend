using Microsoft.Extensions.Options;

namespace Playr.Application.Email;

public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    private static readonly string[] PlaceholderMarkers = ["REPLACE", "CHANGE-ME", "PLACEHOLDER"];

    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        // An unconfigured host is valid: the application falls back to logging
        // confirmation links instead of sending real mail (development mode).
        if (!options.IsConfigured)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (options.Port is <= 0 or > 65535)
        {
            failures.Add("Email:Port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(options.FromAddress))
        {
            failures.Add("Email:FromAddress must be configured when Email:Host is set.");
        }
        else if (IsPlaceholder(options.FromAddress))
        {
            failures.Add("Email:FromAddress still contains a placeholder value.");
        }

        if (IsPlaceholder(options.Password))
        {
            failures.Add("Email:Password still contains a placeholder value.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static bool IsPlaceholder(string value)
    {
        return PlaceholderMarkers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
