using System.Diagnostics;
using OfflineDAoCLauncher.Models;

namespace OfflineDAoCLauncher.Services;

/// <summary>
/// Opens the OfflineDAoC server control panel by running the start script in the install folder:
/// for the client folder <c>C:\OfflineDAoC-True165\new-client\godot</c> that is
/// <c>C:\OfflineDAoC-True165\START OFFLINE DAOC TRUE 1.65.cmd</c>.
/// </summary>
public static class ServerControl
{
    public const string ScriptName = "START OFFLINE DAOC TRUE 1.65.cmd";

    /// <summary>The start script for the entry's install, whether or not it exists; null without a client folder.</summary>
    public static string? ScriptPath(ServerAccount account)
    {
        var client = GameLauncher.FindClientFolder(account.ClientFolder) ?? account.ClientFolder;
        var install = GeneratedAccount.InstallFolder(client);
        return install is null ? null : Path.Combine(install, ScriptName);
    }

    /// <exception cref="LaunchException">The script is missing or could not be started.</exception>
    public static void Launch(ServerAccount account)
    {
        if (!OperatingSystem.IsWindows())
            throw new LaunchException(
                $"The server control panel is started by \"{ScriptName}\", which only runs on Windows.");

        var script = ScriptPath(account) ?? throw new LaunchException(
            $"\"{account.ServerName}\" has no Godot client folder, so the server's install folder is unknown.\n\n" +
            "Set the client folder under Server Accounts.");

        if (!File.Exists(script))
            throw new LaunchException(
                $"Could not find the server start script:\n\n{script}\n\n" +
                "It should be two folders above the Godot client folder (for example " +
                @"C:\OfflineDAoC-True165 for C:\OfflineDAoC-True165\new-client\godot).");

        try
        {
            // Shell-execute the .cmd so its console shows any error message it prints (and its pause).
            Process.Start(new ProcessStartInfo(script)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(script)!,
            })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            throw new LaunchException($"Could not start the server control panel:\n\n{script}\n\n{ex.Message}");
        }
    }
}
