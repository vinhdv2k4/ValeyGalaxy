using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

public static class AuthTokens
{
    public static string? GetTokenHash(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var token = header[7..].Trim();
        return token.Length == 64 ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) : null;
    }

    public static async Task<GameUser?> ResolveUser(HttpRequest request, GameDb db)
    {
        var hash = GetTokenHash(request);
        if (hash == null) return null;
        var session = await db.Sessions.Include(s => s.User).ThenInclude(u => u.Farmer)
            .SingleOrDefaultAsync(s => s.TokenHash == hash && s.ExpiresUtc > DateTime.UtcNow);
        return session?.User;
    }

    public static async Task<Guid?> ResolveUserId(HttpRequest request, GameDb db)
    {
        var hash = GetTokenHash(request);
        if (hash == null) return null;
        return await db.Sessions.AsNoTracking()
            .Where(s => s.TokenHash == hash && s.ExpiresUtc > DateTime.UtcNow)
            .Select(s => (Guid?)s.UserId)
            .SingleOrDefaultAsync();
    }
}
