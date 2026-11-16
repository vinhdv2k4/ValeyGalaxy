using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// SRP: Manages session lifecycle - token issuance, validation, and revocation.
/// Single responsibility: only handles session persistence and token hashing.
/// </summary>
public sealed class SessionService : ISessionService
{
    private const int TokenLengthBytes = 32;
    private const int SessionDays = 7;

    private readonly GameDb _db;

    public SessionService(GameDb db)
    {
        _db = db;
    }

    public async Task<AuthResponse> IssueSessionAsync(GameUser user)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenLengthBytes));
        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(token),
            ExpiresUtc = DateTime.UtcNow.AddDays(SessionDays)
        };

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        return new AuthResponse(token, user.Username, user.Email, user.Farmer?.ToDto());
    }

    public async Task<GameUser?> ResolveUserAsync(string tokenHash)
    {
        var session = await _db.Sessions
            .Include(s => s.User)
            .ThenInclude(u => u.Farmer)
            .SingleOrDefaultAsync(s => s.TokenHash == tokenHash && s.ExpiresUtc > DateTime.UtcNow);

        return session?.User;
    }

    public async Task<bool> RevokeSessionAsync(string tokenHash)
    {
        var session = await _db.Sessions.SingleOrDefaultAsync(s => s.TokenHash == tokenHash);
        if (session == null) return false;

        _db.Sessions.Remove(session);
        await _db.SaveChangesAsync();
        return true;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
