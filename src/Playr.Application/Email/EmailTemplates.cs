using System.Net;

namespace Playr.Application.Email;

public static class EmailTemplates
{
    public static string ConfirmationSubject => "Confirm your PLAYR account";

    public static string ConfirmationBody(string username, string confirmationUrl)
    {
        var safeUsername = WebUtility.HtmlEncode(username);
        var safeUrl = WebUtility.HtmlEncode(confirmationUrl);

        return $"""
            <!DOCTYPE html>
            <html>
              <body style="font-family: Arial, Helvetica, sans-serif; background-color: #0a0710; color: #f2eefb; padding: 32px;">
                <div style="max-width: 480px; margin: 0 auto; background-color: #15101f; border: 1px solid #2a2140; border-radius: 12px; padding: 32px;">
                  <h1 style="color: #8b3dff; letter-spacing: 2px; margin-top: 0;">PLAYR</h1>
                  <p>Hi {safeUsername},</p>
                  <p>Welcome to PLAYR. Confirm your email address to activate your account.</p>
                  <p style="margin: 32px 0;">
                    <a href="{safeUrl}" style="background-color: #8b3dff; color: #ffffff; padding: 12px 24px; border-radius: 8px; text-decoration: none; display: inline-block;">Confirm email</a>
                  </p>
                  <p style="color: #a294c0; font-size: 13px;">If the button does not work, paste this link into your browser:</p>
                  <p style="color: #a294c0; font-size: 13px; word-break: break-all;">{safeUrl}</p>
                  <p style="color: #a294c0; font-size: 13px;">If you did not create this account, you can safely ignore this email.</p>
                </div>
              </body>
            </html>
            """;
    }
}
