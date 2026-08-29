namespace Playr.Application.Auth;

/// <summary>
/// Thrown when credentials are valid but the account's email address has not been confirmed.
/// </summary>
public sealed class EmailNotConfirmedException(string message) : Exception(message);
