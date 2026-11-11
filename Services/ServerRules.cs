using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OfflineDAoCLauncher.Services;

/// <summary>A problem with the rules file, with a message that can be shown to the user as-is.</summary>
public sealed class RulesFileException(string message) : Exception(message);

public enum SettingKind
{
    Bool,
    Double,
    Int,
    UShort,
    Long,
    RegionList,

    /// <summary>A setting the launcher does not know: edited as plain text.</summary>
    Text,
}

/// <summary>
/// One entry in the rules file's <c>settings</c> section. Types and ranges follow the server's
/// <c>[ServerProperty]</c> fields (GameServer/serverproperty/ServerProperties.cs), which read the
/// value with <c>Convert.ChangeType</c> in the en-US culture.
/// </summary>
public sealed record SettingInfo(
    string Key,
    string Group,
    string Label,
    SettingKind Kind,
    string Default,
    string Help,
    long Min = long.MinValue,
    long Max = long.MaxValue)
{
    private static readonly CultureInfo Server = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Returns the value as it should be written, or an error message.</summary>
    public (string? Value, string? Error) Normalize(string? input)
    {
        var text = (input ?? "").Trim();
        switch (Kind)
        {
            case SettingKind.Bool:
                return bool.TryParse(text, out var b) ? (b ? "True" : "False", null) : (null, "must be True or False.");

            case SettingKind.Double:
                if (!double.TryParse(text, NumberStyles.Float, Server, out var d) || !double.IsFinite(d) || text.Contains(','))
                    return (null, "must be a number, for example 1 or 1.5 (use a dot, not a comma).");
                if (d < Min)
                    return (null, $"must be {Min} or more.");
                return (d.ToString("R", Server), null);

            case SettingKind.Int:
            case SettingKind.UShort:
            case SettingKind.Long:
                if (!long.TryParse(text, NumberStyles.AllowLeadingSign, Server, out var n))
                    return (null, "must be a whole number.");
                if (n < Min || n > Max)
                    return (null, Max == long.MaxValue ? $"must be {Min} or more." : $"must be from {Min} to {Max}.");
                return (n.ToString(Server), null);

            case SettingKind.RegionList:
                return NormalizeRegions(text);

            default:
                return (input ?? "", null);
        }
    }

    /// <summary>
    /// Region IDs separated by semicolons, with dashes for ranges (<c>27;165;233-242;244</c>), as the
    /// server's <c>Util.SplitCSV(value, rangeCheck: true)</c> reads them. Region IDs are 0-65535.
    /// </summary>
    private static (string?, string?) NormalizeRegions(string text)
    {
        var parts = text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cleaned = new List<string>();
        foreach (var part in parts)
        {
            var range = part.Split('-', StringSplitOptions.TrimEntries);
            if (range.Length > 2 || range.Any(r => !ushort.TryParse(r, NumberStyles.None, Server, out _)))
                return (null, $"\"{part}\" is not a region ID (0-65535) or a range like 233-242. Separate entries with ;");
            cleaned.Add(string.Join('-', range.Select(r => ushort.Parse(r, Server).ToString(Server))));
        }

        return (string.Join(';', cleaned), null);
    }
}

/// <summary>
/// The <c>settings</c> section of <c>runtime\true165\true165-rules.json</c>. Only that section is ever
/// rewritten; the rest of the (large) file is kept byte for byte.
/// </summary>
public sealed class ServerRules
{
    public const string FileName = "true165-rules.json";
    private const string SectionName = "settings";

    /// <summary>The known settings, with the defaults the 1.65 install ships with.</summary>
    public static readonly IReadOnlyList<SettingInfo> Known =
    [
        new("classic165_mode", "Mode", "True 1.65 Mode", SettingKind.Bool, "True",
            "Classic and Shrouded Isles as in patch 1.65."),
        new("enable_sluaghbinder", "Mode", "Enable Sluaghbinder", SettingKind.Bool, "False",
            "Experimental Hibernian class (not part of patch 1.65)."),

        new("xp_rate", "Rates", "XP Rate", SettingKind.Double, "1",
            "1 is normal, 1.5 is 50% more, 2 is double, 0.5 is half.", Min: 0),
        new("bot_xp_rate", "Rates", "Bot XP Rate", SettingKind.Double, "1",
            "XP rate for the autonomous player bots only.", Min: 0),

        new("freelevel_days_albion", "Classes & Characters", "Free Level Days (Albion)", SettingKind.Int, "-1",
            "Days before a free level. -1 turns it off.", Min: -1, Max: int.MaxValue),
        new("freelevel_days_midgard", "Classes & Characters", "Free Level Days (Midgard)", SettingKind.Int, "-1",
            "Days before a free level. -1 turns it off.", Min: -1, Max: int.MaxValue),
        new("freelevel_days_hibernia", "Classes & Characters", "Free Level Days (Hibernia)", SettingKind.Int, "-1",
            "Days before a free level. -1 turns it off.", Min: -1, Max: int.MaxValue),
        new("allow_train_anywhere", "Classes & Characters", "Allow /train Anywhere", SettingKind.Bool, "False",
            "Lets players open the trainer window anywhere with /train."),
        new("allow_customize_stats_after_creation", "Classes & Characters", "Re-customize Stats", SettingKind.Bool, "False",
            "Allow changing starting stats after character creation."),
        new("allow_customize_face_after_creation", "Classes & Characters", "Re-customize Face", SettingKind.Bool, "False",
            "Allow changing the face after character creation."),
        new("starting_guild", "Classes & Characters", "Starter Guilds", SettingKind.Bool, "False",
            "Put new characters in a starter guild."),

        new("load_buff_tokens", "Items", "Load Buff Tokens", SettingKind.Bool, "False",
            "Load the buff token NPCs and items."),
        new("load_arrow_summoning", "Items", "Load Arrow Summoning", SettingKind.Bool, "False",
            "Load the arrow summoning items."),

        new("guild_buff_xp", "Guild Buffs (%)", "Guild XP Buff", SettingKind.UShort, "0",
            "Extra PvE XP percent.", Min: 0, Max: ushort.MaxValue),
        new("guild_buff_rp", "Guild Buffs (%)", "Guild RP Buff", SettingKind.UShort, "0",
            "Extra realm point percent.", Min: 0, Max: ushort.MaxValue),
        new("guild_buff_bp", "Guild Buffs (%)", "Guild BP Buff", SettingKind.UShort, "0",
            "Extra bounty point percent.", Min: 0, Max: ushort.MaxValue),
        new("guild_buff_crafting", "Guild Buffs (%)", "Guild Crafting Buff", SettingKind.UShort, "0",
            "Crafting speed percent.", Min: 0, Max: ushort.MaxValue),
        new("guild_buff_artifact_xp", "Guild Buffs (%)", "Guild Artifact XP Buff", SettingKind.UShort, "0",
            "Extra artifact XP percent.", Min: 0, Max: ushort.MaxValue),
        new("guild_buff_masterlevel_xp", "Guild Buffs (%)", "Guild Master Level XP Buff", SettingKind.UShort, "0",
            "Extra master level XP percent.", Min: 0, Max: ushort.MaxValue),
        new("capital_city_crafting_skill_gain_bonus", "Guild Buffs (%)", "Capital City Crafting Bonus", SettingKind.Int, "0",
            "Crafting skill gain percent in capital cities.", Min: 0, Max: int.MaxValue),

        new("allow_all_realms_df", "World", "All Realms in Darkness Falls", SettingKind.Bool, "False",
            "Let every realm enter Darkness Falls."),
        new("bot_use_town_teleporters", "World", "Bots Use Town Teleporters", SettingKind.Bool, "False",
            "Has no effect while True 1.65 Mode is on."),
        new("disabled_regions", "World", "Disabled Regions", SettingKind.RegionList, "27;165;233-242;244",
            "Region IDs separated by ; with ranges like 233-242."),

        new("housing_rent_cottage", "Housing Rent (copper)", "Cottage", SettingKind.Long, "200000",
            "", Min: 0),
        new("housing_rent_house", "Housing Rent (copper)", "House", SettingKind.Long, "2000000",
            "", Min: 0),
        new("housing_rent_villa", "Housing Rent (copper)", "Villa", SettingKind.Long, "8000000",
            "", Min: 0),
        new("housing_rent_mansion", "Housing Rent (copper)", "Mansion", SettingKind.Long, "20000000",
            "", Min: 0),
    ];

    private static readonly JsonSerializerOptions WriteOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private ServerRules(string path, List<(string Key, string Value, bool IsString, string Raw)> entries)
    {
        FilePath = path;
        Entries = entries;
    }

    public string FilePath { get; }

    /// <summary>The settings in file order. Raw is the value's JSON text as found in the file.</summary>
    public IReadOnlyList<(string Key, string Value, bool IsString, string Raw)> Entries { get; }

    public static SettingInfo? Describe(string key) => Known.FirstOrDefault(k => k.Key == key);

    /// <summary>
    /// The rules file for a client folder: two levels up from it, then <c>runtime\true165</c>. For the client
    /// folder <c>C:\OfflineDAoC-True165\new-client\godot</c> that is
    /// <c>C:\OfflineDAoC-True165\runtime\true165\true165-rules.json</c>.
    /// </summary>
    public static string? PathFor(string? clientFolder) =>
        GeneratedAccount.InstallFolder(clientFolder) is { } install
            ? Path.Combine(install, "runtime", "true165", FileName)
            : null;

    public static ServerRules Load(string path)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new RulesFileException($"Could not read {path}:\n\n{ex.Message}");
        }

        var (start, end) = FindSection(bytes, path);
        using var doc = JsonDocument.Parse(bytes.AsMemory(start, end - start));
        var entries = doc.RootElement.EnumerateObject()
            .Select(p => (p.Name,
                p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText(),
                p.Value.ValueKind == JsonValueKind.String,
                p.Value.GetRawText()))
            .ToList();
        return new ServerRules(path, entries);
    }

    /// <summary>
    /// Writes new values for the settings section. Keys not in <paramref name="values"/> keep their
    /// value exactly as it was. The file is re-read first, so changes made to the rest of it since
    /// <see cref="Load"/> are kept.
    /// </summary>
    public void Save(IReadOnlyDictionary<string, string> values)
    {
        try
        {
            var bytes = File.ReadAllBytes(FilePath);
            var (start, end) = FindSection(bytes, FilePath);

            var text = Encoding.UTF8.GetString(bytes);
            var newline = text.Contains("\r\n") ? "\r\n" : "\n";
            var outer = LeadingWhitespace(bytes, start);
            var inner = outer + (outer.Length > 0 ? outer : " ");

            var sb = new StringBuilder("{");
            for (var i = 0; i < Entries.Count; i++)
            {
                var (key, _, _, raw) = Entries[i];
                var value = values.TryGetValue(key, out var v) ? JsonSerializer.Serialize(v, WriteOptions) : raw;
                sb.Append(newline).Append(inner)
                    .Append(JsonSerializer.Serialize(key, WriteOptions)).Append(": ").Append(value);
                if (i < Entries.Count - 1)
                    sb.Append(',');
            }

            sb.Append(newline).Append(outer).Append('}');

            var section = Encoding.UTF8.GetBytes(sb.ToString());
            var result = new byte[start + section.Length + (bytes.Length - end)];
            bytes.AsSpan(0, start).CopyTo(result);
            section.CopyTo(result.AsSpan(start));
            bytes.AsSpan(end).CopyTo(result.AsSpan(start + section.Length));

            // Never write a file the server could not read.
            using (JsonDocument.Parse(result, new JsonDocumentOptions { AllowTrailingCommas = false }))
            {
            }

            // Write next to it and swap, so a failure part-way through cannot leave a half-written file.
            var temp = FilePath + ".tmp";
            File.WriteAllBytes(temp, result);
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new RulesFileException($"Could not save {FilePath}:\n\n{ex.Message}");
        }
        catch (JsonException ex)
        {
            throw new RulesFileException($"{FilePath} is not valid JSON, so it was not changed:\n\n{ex.Message}");
        }
    }

    /// <summary>Byte range of the <c>"settings": { ... }</c> object directly under the root.</summary>
    private static (int Start, int End) FindSection(byte[] bytes, string path)
    {
        try
        {
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1)
                {
                    var isSection = reader.ValueTextEquals(SectionName);
                    reader.Read();
                    if (isSection)
                    {
                        if (reader.TokenType != JsonTokenType.StartObject)
                            break;
                        var start = (int)reader.TokenStartIndex;
                        reader.Skip();
                        return (start, (int)reader.TokenStartIndex + 1);
                    }

                    reader.Skip();
                }
            }
        }
        catch (JsonException ex)
        {
            throw new RulesFileException($"{path} is not valid JSON:\n\n{ex.Message}");
        }

        throw new RulesFileException($"{path} has no \"{SectionName}\" section.");
    }

    /// <summary>Indentation of the line holding the byte at <paramref name="index"/>.</summary>
    private static string LeadingWhitespace(byte[] bytes, int index)
    {
        var lineStart = Array.LastIndexOf(bytes, (byte)'\n', index) + 1;
        var n = 0;
        while (lineStart + n < bytes.Length && bytes[lineStart + n] is (byte)' ' or (byte)'\t')
            n++;
        return Encoding.ASCII.GetString(bytes, lineStart, n);
    }

    /// <summary>Copper as the game shows money, e.g. 2000000 -> "200g".</summary>
    public static string FormatCopper(long copper)
    {
        if (copper == 0)
            return "free";

        var parts = new List<string>();
        void Add(long amount, string unit)
        {
            if (amount > 0)
                parts.Add($"{amount:N0}{unit}");
        }

        Add(copper / 10_000_000, "p");
        Add(copper / 10_000 % 1_000, "g");
        Add(copper / 100 % 100, "s");
        Add(copper % 100, "c");
        return string.Join(' ', parts);
    }
}
