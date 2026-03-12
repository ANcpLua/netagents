using System.Text.RegularExpressions;

namespace NetAgents.Skills;

public sealed class SkillLoadException(string message) : Exception(message);

public sealed record SkillMeta(
    string Name,
    string Description,
    IReadOnlyDictionary<string, string> Extra);

public static partial class SkillLoader
{
    [GeneratedRegex(@"^---\r?\n([\s\S]*?)\r?\n---")]
    private static partial Regex FrontmatterPattern();

    /// <summary>
    /// Parse a SKILL.md file and extract YAML frontmatter.
    /// Returns the parsed metadata (name, description, plus any extra fields).
    /// </summary>
    public static async Task<SkillMeta> LoadSkillMdAsync(
        string filePath,
        CancellationToken ct = default)
    {
        string content;
        try
        {
            content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            throw new SkillLoadException($"SKILL.md not found: {filePath}");
        }

        var match = FrontmatterPattern().Match(content);
        if (!match.Success || string.IsNullOrEmpty(match.Groups[1].Value))
            throw new SkillLoadException($"No YAML frontmatter in {filePath}");

        var fields = ParseSimpleYaml(match.Groups[1].Value);

        if (!fields.TryGetValue("name", out var name) || string.IsNullOrEmpty(name))
            throw new SkillLoadException($"Missing 'name' in SKILL.md frontmatter: {filePath}");

        if (!fields.TryGetValue("description", out var description) || string.IsNullOrEmpty(description))
            throw new SkillLoadException($"Missing 'description' in SKILL.md frontmatter: {filePath}");

        var extra = new Dictionary<string, string>(fields, StringComparer.Ordinal);
        extra.Remove("name");
        extra.Remove("description");

        return new SkillMeta(name, description, extra);
    }

    /// <summary>
    /// Minimal YAML parser for flat key: value frontmatter.
    /// We avoid a full YAML dependency -- SKILL.md frontmatter is simple key-value pairs.
    /// </summary>
    internal static Dictionary<string, string> ParseSimpleYaml(string yaml)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in yaml.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var colonIdx = trimmed.IndexOf(':');
            if (colonIdx == -1)
                continue;

            var key = trimmed[..colonIdx].Trim();
            var value = trimmed[(colonIdx + 1)..].Trim();

            // Strip surrounding quotes
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') ||
                 (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            result[key] = value;
        }

        return result;
    }
}
