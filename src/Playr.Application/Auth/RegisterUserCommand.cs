namespace Playr.Application.Auth;

public sealed record RegisterUserCommand(string Email, string Username, string Password);
