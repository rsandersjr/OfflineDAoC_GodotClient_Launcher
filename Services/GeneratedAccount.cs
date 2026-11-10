namespace OfflineDAoCLauncher.Services;

/// <summary>
/// The account the game generates for itself, written to <c>account.txt</c> as:
/// <code>
/// Account: username_here
/// Password: password_here
/// </code>
/// </summary>
public sealed record GeneratedAccount(string AccountName, string Password, string SourceFile)
{
    public const string FileName = "account.txt";
    private const int MaxLines = 50;

    /// <summary>
    /// Reads the game's login file, <c>runtime\account.txt</c> in the install folder. For the client
    /// folder <c>C:\OfflineDAoC-True165\new-client\godot</c> that is
    /// <c>C:\OfflineDAoC-True165\runtime\account.txt</c>. Returns null when it holds no account.
    /// </summary>
    public static GeneratedAccount? Find(string? clientFolder)
    {
        if (string.IsNullOrWhiteSpace(clientFolder))
            return null;

        // client folder -> new-client -> install folder
        var newClient = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(clientFolder)));
        var install = string.IsNullOrEmpty(newClient) ? null : Path.GetDirectoryName(newClient);
        return string.IsNullOrEmpty(install) ? null : Read(Path.Combine(install, "runtime", FileName));
    }

    private static GeneratedAccount? Read(string file)
    {
        try
        {
            if (!File.Exists(file))
                return null;

            string? account = null, password = null;
            foreach (var line in File.ReadLines(file).Take(MaxLines))
            {
                // Split on the first colon only: a password may contain colons of its own.
                var colon = line.IndexOf(':');
                if (colon < 0)
                    continue;

                var key = line[..colon].Trim();
                var value = line[(colon + 1)..].Trim();
                if (key.Equals("Account", StringComparison.OrdinalIgnoreCase))
                    account ??= value;
                else if (key.Equals("Password", StringComparison.OrdinalIgnoreCase))
                    password ??= value;
            }

            return string.IsNullOrEmpty(account) ? null : new GeneratedAccount(account, password ?? "", file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
