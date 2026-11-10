using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OfflineDAoCLauncher.Models;

namespace OfflineDAoCLauncher.Services;

/// <summary>
/// Loads and saves the server accounts as JSON in the per-user config folder
/// (%APPDATA% on Windows, ~/.config on Linux, ~/Library/Application Support on macOS).
/// </summary>
public sealed class AccountStore
{
    public ObservableCollection<ServerAccount> Accounts { get; } = [];
    public Guid? LastSelectedId { get; set; }
    public string FilePath { get; }

    /// <summary>Set when the saved file could not be read and was set aside.</summary>
    public string? LoadWarning { get; private set; }

    private AccountStore(string filePath) => FilePath = filePath;

    /// <summary>Environment variable that points the launcher at a different accounts file.</summary>
    public const string FilePathVariable = "OFFLINEDAOC_LAUNCHER_ACCOUNTS";

    public static string DefaultFilePath =>
        Environment.GetEnvironmentVariable(FilePathVariable) is { Length: > 0 } custom
            ? Path.GetFullPath(custom)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
                "OfflineDAoCLauncher",
                "accounts.json");

    public static AccountStore Load(string? filePath = null)
    {
        var store = new AccountStore(filePath ?? DefaultFilePath);
        if (!File.Exists(store.FilePath))
            return store;

        try
        {
            var file = JsonSerializer.Deserialize(File.ReadAllText(store.FilePath), StoreJsonContext.Default.StoreFile);
            store.LastSelectedId = file?.LastSelectedId;
            foreach (var saved in file?.Accounts ?? [])
            {
                store.Accounts.Add(new ServerAccount
                {
                    Id = saved.Id == Guid.Empty ? Guid.NewGuid() : saved.Id,
                    ServerName = saved.ServerName ?? ServerAccount.DefaultServerName,
                    GodotFolder = saved.GodotFolder ?? "",
                    ClientFolder = saved.ClientFolder ?? "",
                    ServerIp = saved.ServerIp ?? ServerAccount.DefaultServerIp,
                    AccountName = saved.AccountName ?? "",
                    Password = SecretProtector.Unprotect(saved.Password),
                    WorldMode = saved.WorldMode ?? ServerAccount.DefaultWorldMode,
                });
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Keep the unreadable file so nothing is lost, and start with an empty list.
            var backup = store.FilePath + ".unreadable";
            try { File.Copy(store.FilePath, backup, overwrite: true); } catch { /* best effort */ }
            store.LoadWarning =
                $"The saved server accounts could not be read ({ex.Message}).\n\nA copy was kept at:\n{backup}";
        }

        return store;
    }

    public void Save()
    {
        var file = new StoreFile
        {
            LastSelectedId = LastSelectedId,
            Accounts = Accounts.Select(a => new SavedAccount
            {
                Id = a.Id,
                ServerName = a.ServerName,
                GodotFolder = a.GodotFolder,
                ClientFolder = a.ClientFolder,
                ServerIp = a.ServerIp,
                AccountName = a.AccountName,
                Password = SecretProtector.Protect(a.Password),
                WorldMode = a.WorldMode,
            }).ToList(),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(file, StoreJsonContext.Default.StoreFile));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(temp, FilePath, overwrite: true);
    }
}

/// <summary>
/// Keeps passwords out of the JSON file as plain text. On Windows they are encrypted for the
/// current user with DPAPI. Other platforms have no built-in equivalent, so the value is only
/// encoded and the file is restricted to the owner (mode 600).
/// </summary>
internal static class SecretProtector
{
    private const string DpapiPrefix = "dpapi:";
    private const string EncodedPrefix = "b64:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("OfflineDAoCLauncher.v1");

    public static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain))
            return "";

        var bytes = Encoding.UTF8.GetBytes(plain);
        if (OperatingSystem.IsWindows())
            return DpapiPrefix + Convert.ToBase64String(
                ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser));

        return EncodedPrefix + Convert.ToBase64String(bytes);
    }

    public static string Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
            return "";

        try
        {
            if (stored.StartsWith(DpapiPrefix, StringComparison.Ordinal))
            {
                if (!OperatingSystem.IsWindows())
                    return "";
                var data = Convert.FromBase64String(stored[DpapiPrefix.Length..]);
                return Encoding.UTF8.GetString(
                    ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser));
            }

            if (stored.StartsWith(EncodedPrefix, StringComparison.Ordinal))
                return Encoding.UTF8.GetString(Convert.FromBase64String(stored[EncodedPrefix.Length..]));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // Saved by another user or on another machine: the password has to be typed again.
        }

        return "";
    }
}

internal sealed class StoreFile
{
    public Guid? LastSelectedId { get; set; }
    public List<SavedAccount> Accounts { get; set; } = [];
}

internal sealed class SavedAccount
{
    public Guid Id { get; set; }
    public string? ServerName { get; set; }
    public string? GodotFolder { get; set; }
    public string? ClientFolder { get; set; }
    public string? ServerIp { get; set; }
    public string? AccountName { get; set; }
    public string? Password { get; set; }
    public string? WorldMode { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(StoreFile))]
internal sealed partial class StoreJsonContext : JsonSerializerContext;
