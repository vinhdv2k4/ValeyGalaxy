/// <summary>
/// ISP & DIP: Contract for authentication operations.
/// Decouples endpoint handlers from the concrete auth implementation.
/// </summary>
public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task<AccountResponse?> GetCurrentUserAsync(string? tokenHash);
    Task<bool> LogoutAsync(string? tokenHash);
}

/// <summary>Discriminated result for auth operations.</summary>
public sealed class AuthResult
{
    public bool Success { get; }
    public AuthResponse? Response { get; }
    public string? Error { get; }
    public int StatusCode { get; }

    private AuthResult(bool success, AuthResponse? response, string? error, int statusCode)
    {
        Success = success;
        Response = response;
        Error = error;
        StatusCode = statusCode;
    }

    public static AuthResult Ok(AuthResponse response) => new(true, response, null, 200);
    public static AuthResult BadRequest(string error) => new(false, null, error, 400);
    public static AuthResult Conflict(string error) => new(false, null, error, 409);
    public static AuthResult Unauthorized() => new(false, null, "Invalid login or expired session.", 401);
}
