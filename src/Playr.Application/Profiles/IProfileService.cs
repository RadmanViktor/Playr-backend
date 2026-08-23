namespace Playr.Application.Profiles;

public interface IProfileService
{
    Task<ProfileDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<ProfileDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<ProfileDto> UpdateCurrentUserAsync(Guid userId, UpdateProfileCommand command, CancellationToken cancellationToken);
    Task<ProfileDto> UpdateStatusAsync(Guid userId, UpdateStatusCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProfileSearchResult>> SearchAsync(string query, CancellationToken cancellationToken);
    Task<IReadOnlyList<LookingForGamePlayerDto>> GetLookingForGamePlayersAsync(Guid currentUserId, CancellationToken cancellationToken);
}
