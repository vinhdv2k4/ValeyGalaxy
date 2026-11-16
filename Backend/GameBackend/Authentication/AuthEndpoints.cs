/// <summary>
/// SRP: Maps all /api/auth/* and /api/farmer endpoints.
/// Thin routing layer - delegates all logic to IAuthService.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuth(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (RegisterRequest request, IAuthService auth) =>
        {
            var result = await auth.RegisterAsync(request);
            return result.Success
                ? Results.Ok(result.Response)
                : result.StatusCode == 409
                    ? Results.Conflict(new ApiError(result.Error!))
                    : Results.BadRequest(new ApiError(result.Error!));
        }).RequireRateLimiting("auth");

        app.MapPost("/api/auth/login", async (LoginRequest request, IAuthService auth) =>
        {
            var result = await auth.LoginAsync(request);
            return result.Success
                ? Results.Ok(result.Response)
                : Results.Unauthorized();
        }).RequireRateLimiting("auth");

        app.MapGet("/api/auth/me", async (HttpRequest request, IAuthService auth) =>
        {
            var tokenHash = AuthTokens.GetTokenHash(request);
            var account = await auth.GetCurrentUserAsync(tokenHash);
            return account == null ? Results.Unauthorized() : Results.Ok(account);
        });

        app.MapPost("/api/auth/logout", async (HttpRequest request, IAuthService auth) =>
        {
            var tokenHash = AuthTokens.GetTokenHash(request);
            var success = await auth.LogoutAsync(tokenHash);
            return success ? Results.NoContent() : Results.Unauthorized();
        });
    }
}
