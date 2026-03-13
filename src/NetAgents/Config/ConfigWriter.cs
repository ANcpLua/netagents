namespace NetAgents.Config;

using System.Text;
using System.Text.RegularExpressions;

public sealed record DefaultConfigOptions(
    IReadOnlyList<string>? Agents = null,
    TrustConfig? Trust = null,
    IReadOnlyList<SkillEntry>? Skills = null);

public sealed record SkillEntry(string Name, string Source, string? Ref = null, string? Path = null);

public static partial class ConfigWriter
{
    // ── Add skill ────────────────────────────────────────────────────────────────

    public static async Task AddSkillToConfigAsync(
        string filePath,
        string name,
        string source,
        string? @ref = null,
        string? path = null,
        CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine("[[skills]]");
        sb.AppendLine($"name = {TomlQuote(name)}");
        sb.AppendLine($"source = {TomlQuote(source)}");
        if (@ref is not null) sb.AppendLine($"ref = {TomlQuote(@ref)}");
        if (path is not null) sb.AppendLine($"path = {TomlQuote(path)}");

        var newContent = $"{content.TrimEnd()}\n\n{sb.ToString().TrimEnd()}\n";
        await File.WriteAllTextAsync(filePath, newContent, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    // ── Remove skill ─────────────────────────────────────────────────────────────

    public static async Task RemoveSkillFromConfigAsync(
        string filePath,
        string name,
        CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var result = RemoveBlockByHeader(content, "[[skills]]", name);
        await File.WriteAllTextAsync(filePath, result, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    // ── Add wildcard ─────────────────────────────────────────────────────────────

    public static async Task AddWildcardToConfigAsync(
        string filePath,
        string source,
        string? @ref = null,
        IReadOnlyList<string>? exclude = null,
        CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine("[[skills]]");
        sb.AppendLine("name = \"*\"");
        sb.AppendLine($"source = {TomlQuote(source)}");
        if (@ref is not null) sb.AppendLine($"ref = {TomlQuote(@ref)}");
        if (exclude is { Count: > 0 }) sb.AppendLine($"exclude = {TomlArray(exclude)}");

        var newContent = $"{content.TrimEnd()}\n\n{sb.ToString().TrimEnd()}\n";
        await File.WriteAllTextAsync(filePath, newContent, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    // ── Add exclude to wildcard ──────────────────────────────────────────────────

    [GeneratedRegex(@"^name\s*=\s*""\*""")]
    private static partial Regex WildcardNamePattern();

    [GeneratedRegex(@"^source\s*=\s*""([^""]+)""")]
    private static partial Regex SourceValuePattern();

    [GeneratedRegex(@"^(exclude\s*=\s*)\[([^\]]*)\]")]
    private static partial Regex ExcludeArrayPattern();

    public static async Task AddExcludeToWildcardAsync(
        string filePath,
        string source,
        string skillName,
        CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var lines = content.Split('\n');
        var result = new List<string>();
        var i = 0;
        var found = false;

        while (i < lines.Length)
        {
            if (lines[i].Trim() == "[[skills]]")
            {
                var blockLines = new List<string> { lines[i] };
                i++;
                while (i < lines.Length && lines[i].Trim() is { Length: > 0 } trimmed && !trimmed.StartsWith('['))
                {
                    blockLines.Add(lines[i]);
                    i++;
                }

                var nameLine = blockLines.FirstOrDefault(l => l.Trim().StartsWith("name"));
                var sourceLine = blockLines.FirstOrDefault(l => l.Trim().StartsWith("source"));
                var isWildcard = nameLine is not null && WildcardNamePattern().IsMatch(nameLine.Trim());
                var sourceMatch = sourceLine is not null ? SourceValuePattern().Match(sourceLine.Trim()) : null;

                if (isWildcard && sourceMatch is { Success: true } && sourceMatch.Groups[1].Value == source && !found)
                {
                    found = true;
                    var excludeIdx = blockLines.FindIndex(l => l.Trim().StartsWith("exclude"));
                    if (excludeIdx >= 0)
                    {
                        var match = ExcludeArrayPattern().Match(blockLines[excludeIdx].Trim());
                        if (match.Success)
                        {
                            var existing = match.Groups[2].Value.Trim();
                            var quoted = TomlQuote(skillName);
                            blockLines[excludeIdx] = existing.Length > 0
                                ? $"{match.Groups[1].Value}[{existing}, {quoted}]"
                                : $"{match.Groups[1].Value}[{quoted}]";
                        }
                    }
                    else
                    {
                        blockLines.Add($"exclude = {TomlArray([skillName])}");
                    }

                    result.AddRange(blockLines);
                    continue;
                }

                result.AddRange(blockLines);
                continue;
            }

            result.Add(lines[i]);
            i++;
        }

        await File.WriteAllTextAsync(filePath, string.Join("\n", result), Encoding.UTF8, ct).ConfigureAwait(false);
    }

    // ── Add MCP ──────────────────────────────────────────────────────────────────

    public static async Task AddMcpToConfigAsync(
        string filePath,
        McpConfig entry,
        CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine("[[mcp]]");
        sb.AppendLine($"name = {TomlQuote(entry.Name)}");

        if (entry.Command is not null)
        {
            sb.AppendLine($"command = {TomlQuote(entry.Command)}");
            if (entry.Args is { Count: > 0 })
                sb.AppendLine($"args = {TomlArray(entry.Args)}");
        }

        if (entry.Url is not null)
        {
            sb.AppendLine($"url = {TomlQuote(entry.Url)}");
            if (entry.Headers is { Count: > 0 })
            {
                var pairs = entry.Headers.Select(h => $"{TomlBareOrQuotedKey(h.Key)} = {TomlQuote(h.Value)}");
                sb.AppendLine($"headers = {{ {string.Join(", ", pairs)} }}");
            }
        }

        if (entry.Env.Count > 0)
            sb.AppendLine($"env = {TomlArray(entry.Env)}");

        var newContent = $"{content.TrimEnd()}\n\n{sb.ToString().TrimEnd()}\n";
        await File.WriteAllTextAsync(filePath, newContent, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    // ── Remove MCP ───────────────────────────────────────────────────────────────

    public static async Task RemoveMcpFromConfigAsync(
        string filePath,
        string name,
        CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var result = RemoveBlockByHeader(content, "[[mcp]]", name);
        await File.WriteAllTextAsync(filePath, result, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    // ── Add trust source ─────────────────────────────────────────────────────────

    [GeneratedRegex(@"^(\s*)")]
    private static partial Regex LeadingWhitespace();

    public static async Task AddTrustSourceAsync(
        string filePath,
        string field,
        string value,
        CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var lines = content.Split('\n').ToList();
        var trustSectionIdx = -1;
        var fieldLineIdx = -1;

        // First pass: find [trust] section and target field
        for (var j = 0; j < lines.Count; j++)
            if (lines[j].Trim() == "[trust]")
            {
                trustSectionIdx = j;
            }
            else if (trustSectionIdx >= 0 && fieldLineIdx < 0)
            {
                var trimmed = lines[j].Trim();
                if (trimmed.StartsWith('[')) break;
                if (trimmed.StartsWith($"{field} ") || trimmed.StartsWith($"{field}="))
                    fieldLineIdx = j;
            }

        if (trustSectionIdx >= 0 && fieldLineIdx >= 0)
        {
            // Field exists -- parse and append
            var line = lines[fieldLineIdx];
            var indent = LeadingWhitespace().Match(line).Groups[1].Value;
            var trimmedLine = line.Trim();
            var fieldPattern = new Regex($@"^({Regex.Escape(field)}\s*=\s*)\[([^\]]*)\]");
            var match = fieldPattern.Match(trimmedLine);
            if (match.Success)
            {
                var existing = match.Groups[2].Value.Trim();
                var newVal = TomlQuote(value);
                var updated = existing.Length > 0
                    ? $"{match.Groups[1].Value}[{existing}, {newVal}]"
                    : $"{match.Groups[1].Value}[{newVal}]";
                lines[fieldLineIdx] = indent + updated;
            }

            await File.WriteAllTextAsync(filePath, string.Join("\n", lines), Encoding.UTF8, ct).ConfigureAwait(false);
            return;
        }

        if (trustSectionIdx >= 0)
        {
            // [trust] exists but field doesn't -- insert at end of trust section
            var insertIdx = trustSectionIdx + 1;
            while (insertIdx < lines.Count)
            {
                var trimmed = lines[insertIdx].Trim();
                if (trimmed.StartsWith('[')) break;
                if (trimmed == "" && insertIdx + 1 < lines.Count && lines[insertIdx + 1].Trim().StartsWith('[')) break;
                insertIdx++;
            }

            lines.Insert(insertIdx, $"{field} = {TomlArray([value])}");
            await File.WriteAllTextAsync(filePath, string.Join("\n", lines), Encoding.UTF8, ct).ConfigureAwait(false);
            return;
        }

        // No [trust] section -- create one before [[skills]]/[[mcp]]/[[hooks]], or at end
        var arrayTableIdx = lines.FindIndex(l =>
        {
            var t = l.Trim();
            return t is "[[skills]]" or "[[mcp]]" or "[[hooks]]";
        });

        if (arrayTableIdx >= 0)
        {
            lines.InsertRange(arrayTableIdx, ["[trust]", $"{field} = {TomlArray([value])}", ""]);
            await File.WriteAllTextAsync(filePath, string.Join("\n", lines), Encoding.UTF8, ct).ConfigureAwait(false);
            return;
        }

        var newContent = $"{content.TrimEnd()}\n\n[trust]\n{field} = {TomlArray([value])}\n";
        await File.WriteAllTextAsync(filePath, newContent, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    // ── Remove trust source ──────────────────────────────────────────────────────

    public static async Task RemoveTrustSourceAsync(
        string filePath,
        string field,
        string value,
        CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var lines = content.Split('\n').ToList();
        var trustSectionIdx = -1;

        for (var j = 0; j < lines.Count; j++)
            if (lines[j].Trim() == "[trust]")
            {
                trustSectionIdx = j;
            }
            else if (trustSectionIdx >= 0)
            {
                var trimmed = lines[j].Trim();
                if (trimmed.StartsWith('[')) break;

                if (trimmed.StartsWith($"{field} ") || trimmed.StartsWith($"{field}="))
                {
                    var fieldPattern = new Regex($@"^{Regex.Escape(field)}\s*=\s*\[([^\]]*)\]");
                    var match = fieldPattern.Match(trimmed);
                    if (!match.Success) break;

                    var items = match.Groups[1].Value
                        .Split(',')
                        .Select(s => s.Trim())
                        .Where(s => s.Length > 0)
                        .ToList();

                    var filtered = items
                        .Where(s => !string.Equals(
                            s.Trim('"'),
                            value,
                            StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (filtered.Count == 0)
                    {
                        lines.RemoveAt(j);
                        if (IsTrustSectionEmpty(lines, trustSectionIdx))
                            RemoveTrustHeader(lines, trustSectionIdx);
                    }
                    else
                    {
                        var indent = LeadingWhitespace().Match(lines[j]).Groups[1].Value;
                        lines[j] = $"{indent}{field} = [{string.Join(", ", filtered)}]";
                    }

                    await File.WriteAllTextAsync(filePath, string.Join("\n", lines), Encoding.UTF8, ct)
                        .ConfigureAwait(false);
                    return;
                }
            }
    }

    // ── Generate default config ──────────────────────────────────────────────────

    public static string GenerateDefaultConfig(DefaultConfigOptions? opts = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("version = 1");

        if (opts?.Agents is { Count: > 0 } agents)
        {
            var list = string.Join(", ", agents.Select(a => $"\"{a}\""));
            sb.AppendLine($"agents = [{list}]");
        }

        if (opts?.Trust is { } trust)
        {
            if (trust.AllowAll)
            {
                sb.AppendLine();
                sb.AppendLine("[trust]");
                sb.AppendLine("allow_all = true");
            }
            else
            {
                var fields = new List<(string Key, IReadOnlyList<string> Values)>();
                if (trust.GithubOrgs.Count > 0) fields.Add(("github_orgs", trust.GithubOrgs));
                if (trust.GithubRepos.Count > 0) fields.Add(("github_repos", trust.GithubRepos));
                if (trust.GitDomains.Count > 0) fields.Add(("git_domains", trust.GitDomains));

                if (fields.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("[trust]");
                    foreach (var (key, values) in fields)
                        sb.AppendLine($"{key} = {TomlArray(values)}");
                }
            }
        }

        if (opts?.Skills is { Count: > 0 } skills)
            foreach (var skill in skills)
            {
                sb.AppendLine();
                sb.AppendLine("[[skills]]");
                sb.AppendLine($"name = {TomlQuote(skill.Name)}");
                sb.AppendLine($"source = {TomlQuote(skill.Source)}");
                if (skill.Ref is not null) sb.AppendLine($"ref = {TomlQuote(skill.Ref)}");
                if (skill.Path is not null) sb.AppendLine($"path = {TomlQuote(skill.Path)}");
            }

        return sb.ToString();
    }

    // ── Shared helpers ───────────────────────────────────────────────────────────

    private static string RemoveBlockByHeader(string content, string header, string name)
    {
        var lines = content.Split('\n');
        var result = new List<string>();
        var i = 0;

        while (i < lines.Length)
        {
            if (lines[i].Trim() == header)
            {
                var blockLines = new List<string> { lines[i] };
                i++;
                while (i < lines.Length && lines[i].Trim() is { Length: > 0 } trimmed && !trimmed.StartsWith('['))
                {
                    blockLines.Add(lines[i]);
                    i++;
                }

                // Check if this block's name matches
                var nameLine = blockLines.FirstOrDefault(l => l.Trim().StartsWith("name"));
                var match = nameLine is not null
                    ? Regex.Match(nameLine, @"^name\s*=\s*""([^""]+)""")
                    : null;

                if (match is { Success: true } && match.Groups[1].Value == name)
                {
                    // Remove blank lines before the block
                    while (result.Count > 0 && result[^1].Trim() == "")
                        result.RemoveAt(result.Count - 1);
                    // Skip this block
                    continue;
                }

                result.AddRange(blockLines);
                continue;
            }

            result.Add(lines[i]);
            i++;
        }

        return string.Join("\n", result);
    }

    private static bool IsTrustSectionEmpty(List<string> lines, int headerIdx)
    {
        for (var i = headerIdx + 1; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith('[')) return true;
            if (trimmed != "") return false;
        }

        return true;
    }

    private static void RemoveTrustHeader(List<string> lines, int headerIdx)
    {
        var removeCount = 1;
        while (headerIdx + removeCount < lines.Count && lines[headerIdx + removeCount].Trim() == "")
            removeCount++;

        if (headerIdx > 0 && lines[headerIdx - 1].Trim() == "")
            lines.RemoveRange(headerIdx - 1, removeCount + 1);
        else
            lines.RemoveRange(headerIdx, removeCount);
    }

    private static string TomlQuote(string value)
    {
        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }

    private static string TomlArray(IReadOnlyList<string> values)
    {
        return $"[{string.Join(", ", values.Select(TomlQuote))}]";
    }

    /// <summary>
    ///     TOML bare keys only allow A-Za-z0-9, -, _. Anything else must be quoted.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9_-]+$")]
    private static partial Regex BareKeyPattern();

    private static string TomlBareOrQuotedKey(string key)
    {
        return BareKeyPattern().IsMatch(key) ? key : TomlQuote(key);
    }
}
