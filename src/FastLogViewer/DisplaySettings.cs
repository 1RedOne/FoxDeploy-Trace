using System.Text.Json;
using System.Text.RegularExpressions;

namespace FastLogViewer;

internal sealed record HighlightRule(string Text, string ColorName, bool Enabled = true);

internal sealed class DisplaySettings
{
    public bool HideProcessAndThreadIds { get; set; }
    public bool HideNullGuids { get; set; }
    public List<HighlightRule> Rules { get; set; } =
    [
        new("Recieved", "Cyan"),
        new("Received", "Cyan"),
        new("Set", "Orange")
    ];

    public static readonly string[] ColorNames =
        ["Cyan", "Orange", "Yellow", "Green", "Pink", "Lavender", "Blue", "Red"];

    public static Color GetColor(string name) => name switch
    {
        "Cyan" => Color.FromArgb(180, 245, 250),
        "Orange" => Color.FromArgb(255, 211, 153),
        "Yellow" => Color.FromArgb(255, 245, 163),
        "Green" => Color.FromArgb(195, 239, 199),
        "Pink" => Color.FromArgb(255, 209, 229),
        "Lavender" => Color.FromArgb(225, 210, 250),
        "Blue" => Color.FromArgb(195, 222, 255),
        "Red" => Color.FromArgb(255, 185, 185),
        _ => throw new ArgumentException($"Unknown highlight color: {name}", nameof(name))
    };

    public Color MatchColor(ReadOnlySpan<char> line)
    {
        foreach (var rule in Rules)
        {
            if (rule.Enabled && !string.IsNullOrWhiteSpace(rule.Text) &&
                line.Contains(rule.Text, StringComparison.OrdinalIgnoreCase))
            {
                return GetColor(rule.ColorName);
            }
        }
        return SystemColors.Window;
    }

    public static DisplaySettings Load(string path)
    {
        try
        {
            return Deserialize(File.ReadAllText(path));
        }
        catch (FileNotFoundException)
        {
            return new DisplaySettings();
        }
        catch (DirectoryNotFoundException)
        {
            return new DisplaySettings();
        }
    }

    private static DisplaySettings Deserialize(string json)
    {
        var settings = JsonSerializer.Deserialize<DisplaySettings>(json)
            ?? throw new JsonException("Settings must contain an object.");
        if (settings.Rules is null || settings.Rules.Any(rule =>
            rule is null || string.IsNullOrWhiteSpace(rule.Text) || !ColorNames.Contains(rule.ColorName)))
        {
            throw new JsonException("Each highlight rule requires text and a supported color.");
        }
        return settings;
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".settings-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

internal static partial class LogDisplay
{
    public const int PreviewLength = 4096;

    [GeneratedRegex(@"\[(?:PID|TID)\s+[0-9]+\][ \t]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LegacyIds();

    [GeneratedRegex(@"\[0+(?:-0+)+\][ \t]*", RegexOptions.CultureInvariant)]
    private static partial Regex NullGuids();

    public static string Format(ReadOnlySpan<char> line, DisplaySettings settings, bool preview = false)
    {
        var truncated = preview && line.Length > PreviewLength;
        var text = (truncated ? line[..PreviewLength] : line).ToString();
        if (settings.HideProcessAndThreadIds)
        {
            text = LegacyIds().Replace(text, "");
        }
        if (settings.HideNullGuids)
        {
            text = NullGuids().Replace(text, "");
        }
        return truncated ? text + " ... [select row for full text]" : text;
    }
}
