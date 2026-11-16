/// <summary>
/// ISP & DIP: Contract for session management operations.
/// Separates session creation/validation/revocation from auth logic.
/// </summary>
public interface ISessionService
{
    Task<AuthResponse> IssueSessionAsync(GameUser user);
    Task<GameUser?> ResolveUserAsync(string tokenHash);
    Task<bool> RevokeSessionAsync(string tokenHash);
}
