using System.ComponentModel;
using System.Diagnostics;
using OfflineDAoCLauncher.Models;

namespace OfflineDAoCLauncher.Services;

/// <summary>A launch problem with a message that can be shown to the user as-is.</summary>
public sealed class LaunchException(string message) : Exception(message);

/// <summary>
/// Starts the game client:
/// <code>Godot_v4.7.2-stable_mono_win64.exe --path godot -- --server=IP --account=NAME --password=PW --world-mode=1.65</code>
/// The command runs from the folder that contains the client (<c>godot</c>) folder.
/// </summary>
public static class GameLauncher
{
    public const string ProjectFolderName = "godot";
    public const string ProjectFileName = "project.godot";
    private const string PreferredWindowsExe = "Godot_v4.7.2-stable_mono_win64.exe";
    private const int MaxLevelsUp = 4;

    /// <param name="Executable">Full path of the Godot Mono engine.</param>
    /// <param name="WorkingDirectory">Folder the engine is started from: the parent of the client folder.</param>
    /// <param name="ProjectPath">Client folder relative to <paramref name="WorkingDirectory"/>, normally <c>godot</c>.</param>
    public sealed record Installation(string Executable, string WorkingDirectory, string ProjectPath);

    /// <summary>Finds the Godot Mono executable in the picked folder (or just below it).</summary>
    public static string? FindExecutable(string? godotFolder)
    {
        if (string.IsNullOrWhiteSpace(godotFolder) || !Directory.Exists(godotFolder))
            return null;

        var folder = Path.GetFullPath(godotFolder);
        return new[] { folder }
            .Concat(SubFolders(folder, depth: 2))
            .Select(BestExecutableIn)
            .FirstOrDefault(found => found is not null);
    }

    /// <summary>
    /// Returns the client folder for what the user picked: the folder itself when it holds
    /// project.godot, or its <c>godot</c> subfolder when they picked one level too high.
    /// </summary>
    public static string? FindClientFolder(string? clientFolder)
    {
        if (string.IsNullOrWhiteSpace(clientFolder) || !Directory.Exists(clientFolder))
            return null;

        var folder = Path.GetFullPath(clientFolder);
        if (File.Exists(Path.Combine(folder, ProjectFileName)))
            return folder;

        var child = Path.Combine(folder, ProjectFolderName);
        return File.Exists(Path.Combine(child, ProjectFileName)) ? child : null;
    }

    /// <summary>
    /// Looks for a <c>godot</c> client folder in the engine folder and the folders above it.
    /// Only used to suggest a value in the form and for entries saved without a client folder.
    /// </summary>
    public static string? GuessClientFolder(string? godotFolder)
    {
        if (string.IsNullOrWhiteSpace(godotFolder) || !Directory.Exists(godotFolder))
            return null;

        var current = new DirectoryInfo(Path.GetFullPath(godotFolder));
        for (var level = 0; current is not null && level <= MaxLevelsUp; level++, current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, ProjectFolderName);
            if (File.Exists(Path.Combine(candidate, ProjectFileName)))
                return candidate;
        }

        return null;
    }

    public static Installation Resolve(ServerAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.GodotFolder))
            throw new LaunchException(
                $"No Godot Mono folder is set for \"{account.ServerName}\".\n\nOpen Server Accounts and edit this entry.");

        if (!Directory.Exists(account.GodotFolder))
            throw new LaunchException($"The Godot Mono folder no longer exists:\n{account.GodotFolder}");

        var executable = FindExecutable(account.GodotFolder)
            ?? throw new LaunchException(
                $"No Godot Mono executable was found in:\n{account.GodotFolder}\n\n" +
                $"Pick the folder that contains {ExpectedExecutableHint}.");

        string clientFolder;
        if (string.IsNullOrWhiteSpace(account.ClientFolder))
        {
            clientFolder = GuessClientFolder(account.GodotFolder)
                ?? throw new LaunchException(
                    $"No Godot client folder is set for \"{account.ServerName}\".\n\nOpen Server Accounts and edit this entry.");
        }
        else
        {
            if (!Directory.Exists(account.ClientFolder))
                throw new LaunchException($"The Godot client folder no longer exists:\n{account.ClientFolder}");

            clientFolder = FindClientFolder(account.ClientFolder)
                ?? throw new LaunchException(
                    $"No {ProjectFileName} was found in:\n{account.ClientFolder}\n\n" +
                    $"Pick the game's \"{ProjectFolderName}\" folder, the one that holds {ProjectFileName}.");
        }

        // Run from the folder above the client so the command stays "--path godot".
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(clientFolder));
        return string.IsNullOrEmpty(parent)
            ? new Installation(executable, clientFolder, ".")
            : new Installation(executable, parent, Path.GetFileName(Path.TrimEndingDirectorySeparator(clientFolder)));
    }

    public static ProcessStartInfo BuildStartInfo(ServerAccount account)
    {
        var install = Resolve(account);

        var worldMode = account.WorldMode.Trim().TrimStart('-');
        var info = new ProcessStartInfo
        {
            FileName = install.Executable,
            WorkingDirectory = install.WorkingDirectory,
            UseShellExecute = false,
        };

        // ArgumentList quotes each value for the platform, so spaces or symbols in an
        // account name or password cannot break the command line.
        info.ArgumentList.Add("--path");
        info.ArgumentList.Add(install.ProjectPath);
        info.ArgumentList.Add("--");
        info.ArgumentList.Add($"--server={account.ServerIp.Trim()}");
        info.ArgumentList.Add($"--account={account.AccountName.Trim()}");
        info.ArgumentList.Add($"--password={account.Password}");
        if (worldMode.Length > 0)
            info.ArgumentList.Add($"--{worldMode}");

        return info;
    }

    public static void Launch(ServerAccount account)
    {
        var info = BuildStartInfo(account);
        try
        {
            using var process = Process.Start(info);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            throw new LaunchException($"Could not start the game client:\n{info.FileName}\n\n{ex.Message}");
        }
    }

    private static string ExpectedExecutableHint =>
        OperatingSystem.IsWindows() ? PreferredWindowsExe
        : OperatingSystem.IsMacOS() ? "Godot_mono.app"
        : "the Godot_v…_mono_linux executable";

    private static IEnumerable<string> SubFolders(string folder, int depth)
    {
        if (depth == 0)
            yield break;

        string[] children;
        try { children = Directory.GetDirectories(folder); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { yield break; }

        foreach (var child in children)
        {
            var name = Path.GetFileName(child);
            // Never walk into the game project itself (it is huge) or hidden folders.
            if (name.StartsWith('.') || name.Equals(ProjectFolderName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                continue;

            yield return child;
            foreach (var deeper in SubFolders(child, depth - 1))
                yield return deeper;
        }
    }

    private static string? BestExecutableIn(string folder)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
                return MacExecutableIn(folder);

            return Directory.EnumerateFiles(folder, "Godot*")
                .Where(IsEngineBinary)
                .OrderByDescending(Score)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsEngineBinary(string path)
    {
        var name = Path.GetFileName(path);
        var extension = Path.GetExtension(name).ToLowerInvariant();

        if (OperatingSystem.IsWindows())
            // The *_console.exe wrapper only opens a terminal window around the real engine.
            return extension == ".exe" && !name.Contains("console", StringComparison.OrdinalIgnoreCase);

        return extension is ".x86_64" or ".x86_32" or ".arm64" or ".arm32" or "";
    }

    private static int Score(string path)
    {
        var name = Path.GetFileName(path);
        if (name.Equals(PreferredWindowsExe, StringComparison.OrdinalIgnoreCase))
            return 2;
        return name.Contains("mono", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private static string? MacExecutableIn(string folder)
    {
        // The picked folder may be the .app bundle itself or the folder that contains it.
        var bundles = folder.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
            ? [folder]
            : Directory.GetDirectories(folder, "Godot*.app");

        return bundles
            .OrderByDescending(Score)
            .Select(bundle => Path.Combine(bundle, "Contents", "MacOS", "Godot"))
            .FirstOrDefault(File.Exists);
    }
}
