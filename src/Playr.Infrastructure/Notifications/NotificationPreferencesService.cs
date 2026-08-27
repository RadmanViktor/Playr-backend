using Microsoft.EntityFrameworkCore;
using Playr.Application.Notifications;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Notifications;

public sealed class NotificationPreferencesService(PlayrDbContext dbContext) : INotificationPreferencesService
{
    public async Task<NotificationPreferencesDto> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Profile was not found.");

        return new NotificationPreferencesDto(profile.ChatSoundEnabled, profile.ChatBrowserNotificationsEnabled);
    }

    public async Task<NotificationPreferencesDto> UpdateAsync(
        Guid userId,
        UpdateNotificationPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Profile was not found.");

        profile.ChatSoundEnabled = command.ChatSoundEnabled;
        profile.ChatBrowserNotificationsEnabled = command.ChatBrowserNotificationsEnabled;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new NotificationPreferencesDto(profile.ChatSoundEnabled, profile.ChatBrowserNotificationsEnabled);
    }
}
