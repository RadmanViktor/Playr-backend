using Microsoft.Extensions.Logging;
using Playr.Application.Email;

namespace Playr.Infrastructure.Email;

/// <summary>
/// Fallback sender used when no SMTP host is configured. Writes the message to the
/// log so confirmation links remain reachable during local development.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "SMTP is not configured. Email '{Subject}' for {Recipient} was not sent. Body:\n{Body}",
            subject,
            toAddress,
            htmlBody);

        return Task.CompletedTask;
    }
}
