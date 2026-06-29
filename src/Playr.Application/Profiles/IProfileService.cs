namespace Playr.Application.Profiles;

public interface IProfileService
{
    Task<ProfileDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<ProfileDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<ProfileDto> UpdateCurrentUserAsync(Guid userId, UpdateProfileCommand command, CancellationToken cancellationToken);
}
