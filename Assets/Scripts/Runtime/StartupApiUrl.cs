using System;

/// <summary>Release players always use the URL shipped with the game.</summary>
public static class StartupApiUrl
{
    public static string Resolve(string configuredUrl, string savedUrl, string[] arguments, bool allowDevelopmentOverrides)
    {
        string url = configuredUrl;
        if (allowDevelopmentOverrides)
        {
            if (!string.IsNullOrWhiteSpace(savedUrl)) url = savedUrl;
            foreach (string argument in arguments)
                if (argument.StartsWith("-api=", StringComparison.OrdinalIgnoreCase))
                    url = argument.Substring(5).Trim();
        }
        return url.Trim().TrimEnd('/');
    }

    public static bool IsProductionUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && !uri.IsLoopback &&
            string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment) && uri.AbsolutePath == "/";
    }
}
