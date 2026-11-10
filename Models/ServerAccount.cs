namespace OfflineDAoCLauncher.Models;

/// <summary>One row in the server list: a server plus the account used to log in to it.</summary>
public sealed class ServerAccount
{
    public const string DefaultServerName = "Local Host";
    public const string DefaultServerIp = "127.0.0.1";
    public const string DefaultWorldMode = "world-mode=1.65";

    public Guid Id { get; init; } = Guid.NewGuid();
    public string ServerName { get; set; } = DefaultServerName;

    /// <summary>Folder the user picked for the Godot Mono engine.</summary>
    public string GodotFolder { get; set; } = "";

    /// <summary>
    /// The game's Godot project folder (the one holding project.godot). Empty only on entries
    /// saved before this field existed; those fall back to searching near <see cref="GodotFolder"/>.
    /// </summary>
    public string ClientFolder { get; set; } = "";

    public string ServerIp { get; set; } = DefaultServerIp;
    public string AccountName { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>Passed to the client as <c>--&lt;WorldMode&gt;</c>, e.g. <c>world-mode=1.65</c>.</summary>
    public string WorldMode { get; set; } = DefaultWorldMode;

    public ServerAccount Clone() => (ServerAccount)MemberwiseClone();

    /// <summary>What screen readers announce for the list row.</summary>
    public override string ToString() => $"{ServerName}, account {AccountName}";
}
