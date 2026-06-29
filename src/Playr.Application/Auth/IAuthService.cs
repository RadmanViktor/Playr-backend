namespace Playr.Application.Auth;

public interface IAuthService
{
    Task<AuthUserDto> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken);
    Task<AuthResult> LoginAsync(string usernameOrEmail, string password, CancellationToken cancellationToken);
    Task<AuthUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}
