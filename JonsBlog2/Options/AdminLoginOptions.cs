namespace JonsBlog2.Options;

public sealed class AdminLoginOptions
{
    public const string SectionName = "AdminLogin";

    public string Username { get; init; } = string.Empty;

    public string PasswordHash { get; init; } = string.Empty;
}
