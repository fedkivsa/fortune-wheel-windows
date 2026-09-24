using System.Globalization;
using System.IO;
using System.Text;

namespace FortuneWheel;

public static class Ini
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    public static Settings Load(string path) => Parse(File.ReadAllText(path));

    public static Settings Parse(string text)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? current = null;
        int lineNumber = 0;
        foreach (var raw in text.Split('\n'))
        {
            lineNumber++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                string section = line[1..^1].Trim();
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (!sections.TryAdd(section, current)) throw new FormatException($"Duplicate section at line {lineNumber}.");
                continue;
            }
            int split = line.IndexOf('=');
            if (current is null || split < 1) throw new FormatException($"Invalid INI syntax at line {lineNumber}.");
            if (!current.TryAdd(line[..split].Trim(), line[(split + 1)..].Trim()))
                throw new FormatException($"Duplicate key at line {lineNumber}.");
        }
        string Get(string section, string key, string? fallback = null) =>
            sections.TryGetValue(section, out var values) && values.TryGetValue(key, out var value)
                ? value : fallback ?? throw new FormatException($"Missing [{section}] {key}.");
        int Integer(string section, string key, string? fallback = null) =>
            int.TryParse(Get(section, key, fallback), NumberStyles.Integer, Invariant, out int value)
                ? value : throw new FormatException($"[{section}] {key} must be an integer.");
        int? Optional(string section, string key)
        {
            var value = Get(section, key, "");
            return value.Length == 0 ? null : Integer(section, key);
        }
        int count = Integer("Wheel", "Players");
        if (count is < 1 or > 5) throw new FormatException("Players must be from 1 to 5.");
        if (!bool.TryParse(Get("Wheel", "VaryImpulse", "true"), out bool vary))
            throw new FormatException("VaryImpulse must be true or false.");
        var result = new Settings { DefaultForce = Integer("Wheel", "Force", "5"), DefaultDrag = Integer("Wheel", "Drag", "5"), VaryImpulse = vary };
        for (int i = 1; i <= count; i++)
        {
            string section = $"Player{i}";
            var player = new Player { Name = Get(section, "Name"), Force = Optional(section, "Force"), Drag = Optional(section, "Drag") };
            int fields = Integer(section, "Fields");
            if (fields is < 1 or > 5) throw new FormatException($"{section}: Fields must be from 1 to 5.");
            for (int j = 1; j <= fields; j++)
            {
                if (!double.TryParse(Get(section, $"Field{j}Share"), NumberStyles.Float, Invariant, out double share))
                    throw new FormatException($"{section}: Field{j}Share must be a number, using a decimal point.");
                player.Fields.Add(new Field { Name = Get(section, $"Field{j}Name"), Share = share });
            }
            result.Players.Add(player);
        }
        if (result.Validate() is { } error) throw new FormatException(error);
        return result;
    }

    public static string Serialize(Settings settings)
    {
        if (settings.Validate() is { } error) throw new FormatException(error);
        static string Name(string value)
        {
            if (value.Contains('\r') || value.Contains('\n')) throw new FormatException("Names cannot contain line breaks.");
            return value.Trim();
        }
        var text = new StringBuilder("; Fortune Wheel — percentages are within each player's sector.\n; Blank player Force/Drag inherits the defaults. Decimal separator: dot.\n[Wheel]\n");
        text.AppendLine($"Players={settings.Players.Count}");
        text.AppendLine($"Force={settings.DefaultForce}\nDrag={settings.DefaultDrag}\nVaryImpulse={settings.VaryImpulse.ToString().ToLowerInvariant()}");
        for (int i = 0; i < settings.Players.Count; i++)
        {
            var p = settings.Players[i];
            text.AppendLine($"\n[Player{i + 1}]\nName={Name(p.Name)}\nForce={p.Force}\nDrag={p.Drag}\nFields={p.Fields.Count}");
            for (int j = 0; j < p.Fields.Count; j++)
                text.AppendLine($"Field{j + 1}Name={Name(p.Fields[j].Name)}\nField{j + 1}Share={p.Fields[j].Share.ToString("R", Invariant)}");
        }
        return text.ToString();
    }

    public static void Save(string path, Settings settings)
    {
        string text = Serialize(settings);
        string temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
