namespace NetAgents.Lockfile;

using Tomlyn.Parsing;
using Tomlyn.Syntax;

public sealed class LockfileException(string message) : Exception(message);

public static class LockfileLoader
{
    public static async Task<LockfileData?> LoadAsync(string filePath, CancellationToken ct = default)
    {
        string raw;
        try
        {
            raw = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }

        var doc = SyntaxParser.Parse(raw);
        return doc.HasErrors
            ? throw new LockfileException($"Invalid TOML in lockfile: {doc.Diagnostics}")
            : ParseDocument(doc);
    }

    private static LockfileData ParseDocument(DocumentSyntax doc)
    {
        int? version = null;
        for (var i = 0; i < doc.KeyValues.ChildrenCount; i++)
        {
            if (doc.KeyValues.GetChild(i) is not { } kv) continue;
            if (GetKeyName(kv.Key) != "version") continue;

            if (kv.Value is IntegerValueSyntax iv)
                version = (int)iv.Value;
            else
                throw new LockfileException("Invalid lockfile schema: 'version' must be an integer");
        }

        if (version == null)
            throw new LockfileException("Invalid lockfile schema: missing 'version' field");

        if (version != 1)
            throw new LockfileException($"Invalid lockfile schema: unsupported version {version}");

        var skills = new Dictionary<string, LockedSkill>();

        for (var i = 0; i < doc.Tables.ChildrenCount; i++)
        {
            if (doc.Tables.GetChild(i) is not TableSyntax table || table.Name == null) continue;

            var firstPart = BareKeyToString(table.Name.Key);
            if (firstPart != "skills" || table.Name.DotKeys.ChildrenCount != 1) continue;

            if (table.Name.DotKeys.GetChild(0) is not { } dotKey) continue;
            var skillName = BareKeyToString(dotKey.Key);
            if (string.IsNullOrEmpty(skillName)) continue;

            skills[skillName] = ParseSkillTable(skillName, table);
        }

        return new LockfileData(version.Value, skills);
    }

    private static LockedSkill ParseSkillTable(string name, TableSyntax table)
    {
        var fields = new Dictionary<string, string>();
        for (var i = 0; i < table.Items.ChildrenCount; i++)
        {
            if (table.Items.GetChild(i) is not { } kv) continue;
            if (kv.Value is StringValueSyntax { Value: not null } sv)
                fields[GetKeyName(kv.Key)] = sv.Value;
        }

        if (!fields.TryGetValue("source", out var source))
            throw new LockfileException($"Invalid lockfile schema: skill '{name}' missing 'source'");

        if (!fields.TryGetValue("resolved_url", out var resolvedUrl))
            return new LockedLocalSkill(source);

        if (!fields.TryGetValue("resolved_path", out var resolvedPath))
            throw new LockfileException($"Invalid lockfile schema: skill '{name}' missing 'resolved_path'");

        fields.TryGetValue("resolved_ref", out var resolvedRef);
        return new LockedGitSkill(source, resolvedUrl, resolvedPath, resolvedRef);
    }

    private static string GetKeyName(KeySyntax? key)
    {
        return BareKeyToString(key?.Key);
    }

    private static string BareKeyToString(BareKeyOrStringValueSyntax? key)
    {
        return key?.ToString().Trim() ?? "";
    }
}
