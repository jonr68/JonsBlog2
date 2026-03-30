using System.Security.Cryptography;

namespace JonsBlog2.Security;

public static class AdminPasswordHasher
{
    private const string SchemeName = "PBKDF2";
    private const string HashAlgorithmLabel = "SHA256";
    private const int SaltSize = 16;
    private const int SubkeySize = 32;
    private const int MinimumIterations = 100_000;
    public const int DefaultIterations = 210_000;

    public static string HashPassword(string password, int iterations = DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (iterations < MinimumIterations)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), $"Iterations must be at least {MinimumIterations}.");
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var subkey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, System.Security.Cryptography.HashAlgorithmName.SHA256, SubkeySize);

        try
        {
            return $"{SchemeName}${HashAlgorithmLabel}${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(subkey)}";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(subkey);
        }
    }

    public static bool IsHashFormatValid(string passwordHash)
    {
        return TryParseHash(passwordHash, out _, out _, out _);
    }

    public static bool VerifyPassword(string password, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (!TryParseHash(passwordHash, out var iterations, out var salt, out var expectedSubkey))
        {
            return false;
        }

        var actualSubkey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, System.Security.Cryptography.HashAlgorithmName.SHA256, expectedSubkey.Length);

        try
        {
            return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(expectedSubkey);
            CryptographicOperations.ZeroMemory(actualSubkey);
        }
    }

    private static bool TryParseHash(string passwordHash, out int iterations, out byte[] salt, out byte[] expectedSubkey)
    {
        iterations = 0;
        salt = [];
        expectedSubkey = [];

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split('$', StringSplitOptions.None);

        if (parts.Length != 5 ||
            !string.Equals(parts[0], SchemeName, StringComparison.Ordinal) ||
            !string.Equals(parts[1], HashAlgorithmLabel, StringComparison.Ordinal) ||
            !int.TryParse(parts[2], out iterations) ||
            iterations < MinimumIterations)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expectedSubkey = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length < SaltSize || expectedSubkey.Length < SubkeySize)
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(expectedSubkey);
            salt = [];
            expectedSubkey = [];
            return false;
        }

        return true;
    }
}
