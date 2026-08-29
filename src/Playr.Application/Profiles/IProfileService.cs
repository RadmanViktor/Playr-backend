namespace Playr.Application.Profiles;

using Playr.Application.Common;

public interface IProfileService
{
    Task<ProfileDto?> GetByUsernameAsync(string username, Guid? currentUserId, CancellationToken cancellationToken);
    Task<ProfileDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<ProfileDto> UpdateCurrentUserAsync(Guid userId, UpdateProfileCommand command, CancellationToken cancellationToken);
    Task<ProfileDto> UpdateStatusAsync(Guid userId, UpdateStatusCommand command, CancellationToken cancellationToken);
    Task<ProfileDto> UpdateAvatarAsync(Guid userId, string baseUrl, FileUploadInput avatar, CancellationToken cancellationToken);
    Task SetOfflineAsync(Guid userId, CancellationToken cancellationToken);
    Task SetOnlineIfOfflineAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProfileSearchResult>> SearchAsync(string query, CancellationToken cancellationToken);
    Task<IReadOnlyList<LookingForGamePlayerDto>> GetLookingForGamePlayersAsync(Guid currentUserId, CancellationToken cancellationToken);
}
