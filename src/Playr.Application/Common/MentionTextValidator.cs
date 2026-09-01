using System.Text.RegularExpressions;

namespace Playr.Application.Common;

/// <summary>
/// Re-validates client-supplied mention ids against the actual post/comment text, so a
/// stale or manipulated <c>mentionedUserIds</c> list can never produce a notification for
/// a user who isn't genuinely referenced as an exact <c>@username</c> token in the text.
/// </summary>
public static class MentionTextValidator
{
    /// <summary>
    /// Returns the user ids from <paramref name="candidates"/> whose username appears as an
    /// exact, whole <c>@username</c> token in <paramref name="text"/> (case-insensitive).
    /// A username that is only a prefix/substring of a longer token (e.g. "Ty" inside
    /// "@TyyD") does not count as present.
    /// </summary>
    public static IReadOnlyList<Guid> FilterPresentInText(
        string text,
        IEnumerable<(Guid UserId, string Username)> candidates)
    {
        var result = new List<Guid>();
        foreach (var (userId, username) in candidates)
        {
            if (string.IsNullOrEmpty(username))
                continue;

            var pattern = $"@{Regex.Escape(username)}\\b";
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
            {
                result.Add(userId);
            }
        }
        return result;
    }
}
