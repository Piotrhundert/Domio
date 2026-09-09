using System.Security.Cryptography;

namespace Domio.Infrastructure.Authentication;

internal static class PasswordSecurity
{
    private const int PasswordIterations = 210_000;
    private const int SaltSize = 16;
    private const int PasswordHashSize = 32;
    private const string PasswordAlgorithm = "PBKDF2-SHA256";

    public static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            password.Length < 10 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit))
        {
            throw new ArgumentException(
                "Hasło musi mieć co najmniej 10 znaków oraz zawierać małą literę, wielką literę i cyfrę.",
                nameof(password));
        }
    }

    public static string HashPassword(string password)
    {
        ValidatePassword(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, PasswordIterations,
            HashAlgorithmName.SHA256, PasswordHashSize);

        return string.Join(
            '$',
            PasswordAlgorithm,
            PasswordIterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split('$');

            if (parts.Length != 4 ||
                parts[0] != PasswordAlgorithm ||
                !int.TryParse(parts[1], out var iterations) ||
                iterations < 100_000)
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, iterations,
                HashAlgorithmName.SHA256, expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(
                actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
