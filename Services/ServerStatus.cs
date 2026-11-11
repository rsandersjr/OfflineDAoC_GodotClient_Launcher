using System.Net;
using System.Net.Sockets;
using System.Xml.Linq;
using OfflineDAoCLauncher.Models;

namespace OfflineDAoCLauncher.Services;

/// <summary>Where a server listens for game clients, and which file said so.</summary>
public sealed record ServerEndpoint(string Host, int Port, string? ConfigFile);

/// <summary>
/// Tells whether a server is up by opening a TCP connection to its login port, the <c>&lt;Port&gt;</c>
/// in the server's config (10300 by default). The server's UDP port does not answer a probe, so
/// TCP is the only reliable check.
/// </summary>
public static class ServerStatus
{
    public const int DefaultPort = 10300;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Finds the server config of the entry's install: <c>runtime\server\config</c>, two folders up from
    /// the Godot client folder. The file is the one <c>runtime\data\world-mode.txt</c> names
    /// (<c>config=config\serverconfig-165.xml</c>), else <c>serverconfig-165.xml</c>, else <c>serverconfig.xml</c>.
    /// Without one, the entry's Server IP and the default port are used.
    /// </summary>
    public static ServerEndpoint Resolve(ServerAccount account)
    {
        var fallbackHost = string.IsNullOrWhiteSpace(account.ServerIp) ? ServerAccount.DefaultServerIp : account.ServerIp.Trim();
        var clientFolder = GameLauncher.FindClientFolder(account.ClientFolder) ?? account.ClientFolder;
        if (GeneratedAccount.InstallFolder(clientFolder) is not { } install)
            return new ServerEndpoint(fallbackHost, DefaultPort, null);

        var server = Path.Combine(install, "runtime", "server");
        foreach (var file in ConfigCandidates(install, server))
        {
            try
            {
                if (!File.Exists(file))
                    continue;

                var root = XDocument.Load(file).Root?.Element("Server");
                var ip = root?.Element("IP")?.Value.Trim();
                var port = int.TryParse(root?.Element("Port")?.Value.Trim(), out var p) && p is > 0 and < 65536 ? p : DefaultPort;

                // 0.0.0.0 means "every address on this machine": reach it the way the entry does.
                var host = string.IsNullOrEmpty(ip) || ip == "0.0.0.0" || ip == "::" ? fallbackHost : ip;
                return new ServerEndpoint(host, port, file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            {
                // Try the next file.
            }
        }

        return new ServerEndpoint(fallbackHost, DefaultPort, null);
    }

    private static IEnumerable<string> ConfigCandidates(string install, string server)
    {
        var modeFile = Path.Combine(install, "runtime", "data", "world-mode.txt");
        string? named = null;
        try
        {
            if (File.Exists(modeFile))
                named = File.ReadLines(modeFile)
                    .Select(l => l.Trim())
                    .FirstOrDefault(l => l.StartsWith("config=", StringComparison.OrdinalIgnoreCase))?[7..].Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fall through to the usual names.
        }

        if (!string.IsNullOrEmpty(named))
            yield return Path.GetFullPath(Path.Combine(server, named.Replace('\\', Path.DirectorySeparatorChar)));
        yield return Path.Combine(server, "config", "serverconfig-165.xml");
        yield return Path.Combine(server, "config", "serverconfig.xml");
    }

    /// <summary>True when something accepts a TCP connection on the endpoint within two seconds.</summary>
    public static async Task<bool> IsOnline(ServerEndpoint endpoint, CancellationToken cancel = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(Timeout);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(endpoint.Host, endpoint.Port, timeout.Token);
            return true;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or ArgumentException)
        {
            return false;
        }
    }
}
