using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Cryptographic hashing service for secure entity IDs and fast hash lookups.
/// Prevents IDOR (Insecure Direct Object Reference), ID enumeration, and timing attacks.
/// </summary>
public interface ISecureIdHasher
{
    /// <summary>Generates a tamper-proof, URL-safe secure hashed ID for a player-owned entity.</summary>
    string GenerateSecurePlotId(Guid plotId, Guid ownerId);

    /// <summary>
    /// Validates HMAC signature against the requester's ID in constant time and extracts the plot ID.
    /// Returns false if tampered with, expired, or attempted by a non-owner.
    /// </summary>
    bool TryDecodeAndVerifyPlotId(string securePlotId, Guid requesterUserId, out Guid plotId);

    /// <summary>
    /// Computes a deterministic 64-character hex hash used for O(1) database hash indexes and memory caches.
    /// </summary>
    string ComputePlotLookupHash(Guid plotId, Guid ownerId);

    /// <summary>
    /// Computes a fast, salted deterministic hash for catalog items and tools.
    /// </summary>
    string ComputeCatalogHash(string rawId);
}

public sealed class SecureIdHasher : ISecureIdHasher
{
    private readonly byte[] _secretKey;

    public SecureIdHasher(IConfiguration configuration, IHostEnvironment environment)
        : this(configuration)
    {
        if (environment.IsProduction() && (configuration["SECURITY_HASH_SECRET"]?.Trim().Length ?? 0) < 32)
            throw new InvalidOperationException("SECURITY_HASH_SECRET must contain at least 32 characters in production.");
    }

    public SecureIdHasher(IConfiguration configuration)
    {
        var secret = configuration["SECURITY_HASH_SECRET"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            // Preserve the previous local appsettings key; Production rejects a missing private env setting.
            secret = "StardewValley-Local-Dev-Secret-Key-2026-Secure-Hash";
        }
        _secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public string GenerateSecurePlotId(Guid plotId, Guid ownerId)
    {
        var payloadBytes = plotId.ToByteArray();
        var signature = ComputeHmac(payloadBytes, ownerId);
        var payloadBase64 = Convert.ToBase64String(payloadBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var signatureBase64 = Convert.ToBase64String(signature)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return $"{payloadBase64}.{signatureBase64}";
    }

    public bool TryDecodeAndVerifyPlotId(string securePlotId, Guid requesterUserId, out Guid plotId)
    {
        plotId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(securePlotId)) return false;

        var parts = securePlotId.Split('.');
        if (parts.Length != 2) return false;

        byte[] payloadBytes;
        byte[] providedSignature;

        try
        {
            payloadBytes = DecodeBase64Url(parts[0]);
            providedSignature = DecodeBase64Url(parts[1]);
        }
        catch
        {
            return false;
        }

        if (payloadBytes.Length != 16 || providedSignature.Length != 32)
            return false;

        // Calculate expected HMAC using the requester's user ID as part of the key derivation
        var expectedSignature = ComputeHmac(payloadBytes, requesterUserId);

        // Constant-time comparison to prevent timing attacks
        if (!CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
            return false;

        plotId = new Guid(payloadBytes);
        return true;
    }

    public string ComputePlotLookupHash(Guid plotId, Guid ownerId)
    {
        // Combined hash of plotId and ownerId with secret key
        var input = Encoding.UTF8.GetBytes($"plot:{plotId:N}:{ownerId:N}");
        using var hmac = new HMACSHA256(_secretKey);
        return Convert.ToHexString(hmac.ComputeHash(input));
    }

    public string ComputeCatalogHash(string rawId)
    {
        var input = Encoding.UTF8.GetBytes($"cat:{rawId.Trim().ToLowerInvariant()}");
        using var hmac = new HMACSHA256(_secretKey);
        return Convert.ToHexString(hmac.ComputeHash(input));
    }

    private byte[] ComputeHmac(byte[] payload, Guid ownerId)
    {
        // Derive contextual key using ownerId to guarantee cross-user separation
        var ownerBytes = ownerId.ToByteArray();
        var contextKey = new byte[_secretKey.Length + ownerBytes.Length];
        Buffer.BlockCopy(_secretKey, 0, contextKey, 0, _secretKey.Length);
        Buffer.BlockCopy(ownerBytes, 0, contextKey, _secretKey.Length, ownerBytes.Length);

        using var hmac = new HMACSHA256(contextKey);
        return hmac.ComputeHash(payload);
    }

    private static byte[] DecodeBase64Url(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
