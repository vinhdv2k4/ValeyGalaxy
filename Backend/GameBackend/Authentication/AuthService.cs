using Microsoft.EntityFrameworkCore;

/// <summary>
/// SRP & OCP: Concrete auth service implementation.
/// Handles registration, login, profile lookup, and logout.
/// Depends on GameDb (DIP via DI container) and ISessionService for token issuance.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly GameDb _db;
    private readonly ISessionService _sessions;

    public AuthService(GameDb db, ISessionService sessions)
    {
        _db = db;
        _sessions = sessions;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        var validationError = AuthValidation.ValidateRegistration(request);
        if (validationError != null)
            return AuthResult.BadRequest(validationError);

        var username = request.Username!.Trim();
        var email = request.Email!.Trim();
        var userKey = username.ToUpperInvariant();
        var emailKey = email.ToUpperInvariant();

        if (await _db.Users.AnyAsync(u => u.UsernameKey == userKey || u.EmailKey == emailKey))
            return AuthResult.Conflict("Username or email is already registered.");

        var user = new GameUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            UsernameKey = userKey,
            Email = email,
            EmailKey = emailKey,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password!, workFactor: 12),
            CreatedUtc = DateTime.UtcNow
        };

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return AuthResult.Conflict("Username or email is already registered.");
        }

        var session = await _sessions.IssueSessionAsync(user);
        return AuthResult.Ok(session);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        var key = request.Identity?.Trim().ToUpperInvariant() ?? "";
        var user = await _db.Users
            .Include(u => u.Farmer)
            .SingleOrDefaultAsync(u => u.UsernameKey == key || u.EmailKey == key);

        if (user == null || string.IsNullOrEmpty(request.Password) ||
            !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return AuthResult.Unauthorized();

        var session = await _sessions.IssueSessionAsync(user);
        return AuthResult.Ok(session);
    }

    public async Task<AccountResponse?> GetCurrentUserAsync(string? tokenHash)
    {
        if (tokenHash == null) return null;
        var user = await _sessions.ResolveUserAsync(tokenHash);
        if (user == null) return null;
        return new AccountResponse(user.Username, user.Email, user.Farmer?.ToDto());
    }

    public async Task<bool> LogoutAsync(string? tokenHash)
    {
        if (tokenHash == null) return false;
        return await _sessions.RevokeSessionAsync(tokenHash);
    }
}
