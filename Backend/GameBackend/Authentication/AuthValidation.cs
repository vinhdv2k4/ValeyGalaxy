using System.Net.Mail;
using System.Text.RegularExpressions;

/// <summary>
/// SRP: Encapsulates all authentication input validation rules.
/// Single responsibility - only validates, never mutates state.
/// </summary>
public static partial class AuthValidation
{
    private static readonly Regex UsernamePattern = UsernameRegex();

    public static bool IsValidUsername(string? username) =>
        !string.IsNullOrWhiteSpace(username) && UsernamePattern.IsMatch(username);

    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254) return false;
        try
        {
            return new MailAddress(email).Address.Equals(email, StringComparison.OrdinalIgnoreCase)
                   && email.Contains('.');
        }
        catch (FormatException) { return false; }
    }

    public static bool IsValidPassword(string? value) =>
        value is { Length: >= 8 and <= 128 } &&
        value.Any(char.IsUpper) && value.Any(char.IsLower) && value.Any(char.IsDigit);

    public static bool PasswordsMatch(string? password, string? confirm) =>
        password == confirm;

    /// <summary>Validates all registration fields and returns the first error, or null if valid.</summary>
    public static string? ValidateRegistration(RegisterRequest request)
    {
        if (!IsValidUsername(request.Username))
            return "Username must be 3-24 letters, numbers, _ or -.";
        if (!IsValidEmail(request.Email?.Trim()))
            return "Enter a valid email address.";
        if (!IsValidPassword(request.Password))
            return "Password needs 8+ characters, uppercase, lowercase and a number.";
        if (!PasswordsMatch(request.Password, request.ConfirmPassword))
            return "Passwords do not match.";
        return null;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{3,24}$")]
    private static partial Regex UsernameRegex();
}
