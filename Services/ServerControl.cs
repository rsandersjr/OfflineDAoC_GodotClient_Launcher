using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using OfflineDAoCLauncher.Models;

namespace OfflineDAoCLauncher.Services;

/// <summary>What <see cref="ServerControl.Open"/> did.</summary>
public enum ServerControlResult
{
    /// <summary>The control panel was not running; the start script was run.</summary>
    Started,

    /// <summary>The control panel was already running and its window was brought to the front.</summary>
    BroughtToFront,

    /// <summary>The control panel is already running, but its window could not be brought to the front.</summary>
    AlreadyRunning,
}

/// <summary>
/// Opens the OfflineDAoC server control panel ("Offline DAoC Launcher and Population Monitor",
/// <c>runtime\OfflineDAoC.exe</c>). When it is already running its window is brought to the front;
/// otherwise the install's start script is run: for the client folder
/// <c>C:\OfflineDAoC-True165\new-client\godot</c> that is
/// <c>C:\OfflineDAoC-True165\START OFFLINE DAOC TRUE 1.65.cmd</c>.
/// </summary>
/// <remarks>
/// The control panel is a Windows desktop program, so on Linux and macOS it runs under Wine. There the
/// running copy is found by its <c>OfflineDAoC.exe</c> command line and focused with xdotool or wmctrl
/// (Linux, X11) or System Events (macOS); the script is started with <c>wine cmd /c</c>.
/// The panel is found by its program file, not its window title, because the title changes between versions.
/// </remarks>
public static class ServerControl
{
    public const string ScriptName = "START OFFLINE DAOC TRUE 1.65.cmd";
    private const string ProcessName = "OfflineDAoC"; // runtime\OfflineDAoC.exe

    // OfflineDAoC.exe as a whole path part in a command line; OfflineDAoCLauncher.exe does not match.
    private static readonly Regex WineCommandLine =
        new(@"(^|[\\/\s""'])OfflineDAoC\.exe(\s|""|'|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>The start script for the entry's install, whether or not it exists; null without a client folder.</summary>
    public static string? ScriptPath(ServerAccount account)
    {
        var install = InstallFolder(account);
        return install is null ? null : Path.Combine(install, ScriptName);
    }

    private static string? InstallFolder(ServerAccount account) =>
        GeneratedAccount.InstallFolder(GameLauncher.FindClientFolder(account.ClientFolder) ?? account.ClientFolder);

    /// <exception cref="LaunchException">The script is missing or could not be started.</exception>
    public static ServerControlResult Open(ServerAccount account)
    {
        var install = InstallFolder(account);
        var running = FindRunning(install);
        if (running.Count > 0)
            return running.Any(BringToFront) ? ServerControlResult.BroughtToFront : ServerControlResult.AlreadyRunning;

        Start(account, install);
        return ServerControlResult.Started;
    }

    private static void Start(ServerAccount account, string? install)
    {
        if (install is null)
            throw new LaunchException(
                $"\"{account.ServerName}\" has no Godot client folder, so the server's install folder is unknown.\n\n" +
                "Set the client folder under Server Accounts.");

        var script = Path.Combine(install, ScriptName);
        if (!File.Exists(script))
            throw new LaunchException(
                $"Could not find the server start script:\n\n{script}\n\n" +
                "It should be two folders above the Godot client folder (for example " +
                @"C:\OfflineDAoC-True165 for C:\OfflineDAoC-True165\new-client\godot).");

        ProcessStartInfo start;
        if (OperatingSystem.IsWindows())
        {
            // Shell-execute the .cmd so its console shows any error message it prints (and its pause).
            start = new ProcessStartInfo(script) { UseShellExecute = true };
        }
        else
        {
            var wine = FindOnPath("wine") ?? FindOnPath("wine64") ?? throw new LaunchException(
                "The OfflineDAoC server control panel is a Windows program. On " +
                (OperatingSystem.IsMacOS() ? "macOS" : "Linux") +
                " it runs under Wine, but \"wine\" was not found.\n\nInstall Wine, then try again.");
            start = new ProcessStartInfo(wine) { UseShellExecute = false };
            start.ArgumentList.Add("cmd");
            start.ArgumentList.Add("/c");
            start.ArgumentList.Add(ScriptName);
        }
        start.WorkingDirectory = install;

        try
        {
            Process.Start(start)?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException)
        {
            throw new LaunchException($"Could not start the server control panel:\n\n{script}\n\n{ex.Message}");
        }
    }

    /// <summary>
    /// Running control panels, those from <paramref name="install"/> first. A copy from another install
    /// counts too: it uses the same server port, so starting a second one would not work anyway.
    /// </summary>
    private static List<int> FindRunning(string? install)
    {
        var found = new List<(int Pid, bool SameInstall)>();
        var runtimeExe = install is null ? null : Path.Combine(install, "runtime", ProcessName + ".exe");

        if (OperatingSystem.IsWindows())
        {
            foreach (var p in Process.GetProcessesByName(ProcessName))
            {
                using (p)
                {
                    string? path = null;
                    try { path = p.MainModule?.FileName; }
                    catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { /* elevated or exiting */ }
                    found.Add((p.Id, path is not null && runtimeExe is not null &&
                                     string.Equals(path, runtimeExe, StringComparison.OrdinalIgnoreCase)));
                }
            }
        }
        else
        {
            // Under Wine the process is wine's loader; only its command line names OfflineDAoC.exe.
            foreach (var (pid, args) in UnixProcesses())
                if (pid != Environment.ProcessId && WineCommandLine.IsMatch(args))
                    found.Add((pid, false));
        }

        return found.OrderByDescending(f => f.SameInstall).Select(f => f.Pid).ToList();
    }

    private static bool BringToFront(int pid)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return Win32.BringToFront(pid);
            if (OperatingSystem.IsMacOS())
                return Run("osascript", "-e",
                    $"tell application \"System Events\" to set frontmost of (first process whose unix id is {pid}) to true");

            // Linux (X11). Wine sets _NET_WM_PID on its windows, so xdotool can find them by process.
            return Run("xdotool", "search", "--onlyvisible", "--pid", pid.ToString(), "windowactivate")
                   || Run("wmctrl", "-a", "OfflineDAoC");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }

    /// <summary>Every process with its full command line, from <c>ps</c> (the same options work on Linux and macOS).</summary>
    private static List<(int Pid, string Args)> UnixProcesses()
    {
        var result = new List<(int, string)>();
        string output;
        try
        {
            var ps = new ProcessStartInfo("ps") { RedirectStandardOutput = true, UseShellExecute = false };
            foreach (var arg in new[] { "-e", "-ww", "-o", "pid=", "-o", "args=" })
                ps.ArgumentList.Add(arg);
            using var p = Process.Start(ps)!;
            output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return result;
        }

        foreach (var line in output.Split('\n'))
        {
            var text = line.TrimStart();
            var space = text.IndexOf(' ');
            if (space > 0 && int.TryParse(text[..space], out var pid))
                result.Add((pid, text[(space + 1)..]));
        }
        return result;
    }

    /// <summary>Runs a helper tool and reports whether it succeeded; false when the tool is not installed.</summary>
    private static bool Run(string tool, params string[] args)
    {
        if (FindOnPath(tool) is not { } path)
            return false;

        var start = new ProcessStartInfo(path) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        using var p = Process.Start(start)!;
        if (!p.WaitForExit(5000))
        {
            try { p.Kill(); } catch (InvalidOperationException) { }
            return false;
        }
        return p.ExitCode == 0;
    }

    private static string? FindOnPath(string tool)
    {
        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Concat(OperatingSystem.IsMacOS()
                ? new[] { "/opt/homebrew/bin", "/usr/local/bin", "/Applications/Wine Stable.app/Contents/Resources/wine/bin" }
                : Array.Empty<string>());
        return dirs.Select(d => Path.Combine(d, tool)).FirstOrDefault(File.Exists);
    }

    private static class Win32
    {
        private const int SwRestore = 9;
        private const uint GwOwner = 4;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>Restores (if minimized) and activates the process's visible top-level window.</summary>
        public static bool BringToFront(int pid)
        {
            var window = IntPtr.Zero;
            EnumWindows((hWnd, _) =>
            {
                if (GetWindowThreadProcessId(hWnd, out var owner) == 0 || owner != pid ||
                    !IsWindowVisible(hWnd) || GetWindow(hWnd, GwOwner) != IntPtr.Zero || GetWindowTextLength(hWnd) == 0)
                    return true;
                window = hWnd;
                return false;
            }, IntPtr.Zero);

            if (window == IntPtr.Zero)
                return false;
            if (IsIconic(window))
                ShowWindow(window, SwRestore);
            // Allowed because the launcher is the foreground app: the user just clicked its button.
            return SetForegroundWindow(window);
        }
    }
}
