using System.Text.RegularExpressions;

namespace NetAgents.Config;

public static partial class SourcePatterns
{
    [GeneratedRegex(@"^git:(https://|git://|ssh://|git@|file://|/)")]
    public static partial Regex GitUrlValid();

    [GeneratedRegex(@"^https?://github\.com/([a-zA-Z0-9][^/]*)\/([a-zA-Z0-9][^/@]*?)(?:\.git)?(?:/)?(?:@(.+))?$")]
    public static partial Regex GithubHttpsUrl();

    [GeneratedRegex(@"^git@github\.com:([a-zA-Z0-9][^/]*)\/([a-zA-Z0-9][^/@]*?)(?:\.git)?(?:@(.+))?$")]
    public static partial Regex GithubSshUrl();

    [GeneratedRegex(@"^https?://gitlab\.com/([a-zA-Z0-9][^@]*?)\/([a-zA-Z0-9][^/@]*?)(?:\.git)?(?:/)?(?:@(.+))?$")]
    public static partial Regex GitlabHttpsUrl();

    [GeneratedRegex(@"^git@gitlab\.com:([a-zA-Z0-9][^@]*?)\/([a-zA-Z0-9][^/@]*?)(?:\.git)?(?:@(.+))?$")]
    public static partial Regex GitlabSshUrl();

    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9._-]*$")]
    public static partial Regex ValidSkillName();
}

// ── Skill dependencies ────────────────────────────────────────────────────────

public abstract record SkillDependency(string Source, string? Ref);

public sealed record WildcardSkillDependency(
    string Source,
    string? Ref,
    IReadOnlyList<string> Exclude)
    : SkillDependency(Source, Ref);

public sealed record RegularSkillDependency(
    string Name,
    string Source,
    string? Ref,
    string? Path)
    : SkillDependency(Source, Ref);

// ── MCP ───────────────────────────────────────────────────────────────────────

public sealed record McpConfig(
    string Name,
    string? Command,
    IReadOnlyList<string>? Args,
    string? Url,
    IReadOnlyDictionary<string, string>? Headers,
    IReadOnlyList<string> Env);

// ── Hooks ─────────────────────────────────────────────────────────────────────

public enum HookEvent { PreToolUse, PostToolUse, UserPromptSubmit, Stop }

public sealed record HookConfig(HookEvent Event, string? Matcher, string Command);

// ── Trust ─────────────────────────────────────────────────────────────────────

public sealed record TrustConfig(
    bool AllowAll,
    IReadOnlyList<string> GithubOrgs,
    IReadOnlyList<string> GithubRepos,
    IReadOnlyList<string> GitDomains);

// ── Project / symlinks ────────────────────────────────────────────────────────

public sealed record ProjectConfig(string? Name);

public sealed record SymlinksConfig(IReadOnlyList<string> Targets);

// ── Repository source ─────────────────────────────────────────────────────────

public enum RepositorySource { Github, Gitlab }

// ── Root config ───────────────────────────────────────────────────────────────

public sealed record AgentsConfig(
    int Version,
    RepositorySource DefaultRepositorySource,
    ProjectConfig? Project,
    SymlinksConfig? Symlinks,
    IReadOnlyList<string> Agents,
    IReadOnlyList<SkillDependency> Skills,
    IReadOnlyList<McpConfig> Mcp,
    IReadOnlyList<HookConfig> Hooks,
    TrustConfig? Trust);

// ── Exception ─────────────────────────────────────────────────────────────────

public sealed class ConfigException(string message) : Exception(message);

// ── Helpers ───────────────────────────────────────────────────────────────────

public static class SkillDependencyHelpers
{
    public static bool IsWildcardDep(SkillDependency dep) => dep is WildcardSkillDependency;

    public static bool IsValidSkillName(string name) =>
        SourcePatterns.ValidSkillName().IsMatch(name);

    public static bool IsValidSkillSource(string source)
    {
        if (source.StartsWith("git:", StringComparison.Ordinal))
            return SourcePatterns.GitUrlValid().IsMatch(source);

        if (source.StartsWith("path:", StringComparison.Ordinal))
            return true;

        if (SourcePatterns.GithubHttpsUrl().IsMatch(source)) return true;
        if (SourcePatterns.GithubSshUrl().IsMatch(source)) return true;
        if (SourcePatterns.GitlabHttpsUrl().IsMatch(source)) return true;
        if (SourcePatterns.GitlabSshUrl().IsMatch(source)) return true;

        // Plain owner/repo[@ref] shorthand
        var atIdx = source.IndexOf('@');
        var base_ = atIdx >= 0 ? source[..atIdx] : source;
        var parts = base_.Split('/');
        return parts is [{ Length: > 0 }, _] && !parts[0].StartsWith('-')
                                             && parts[1].Length > 0 && !parts[1].StartsWith('-');
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public static class AgentsConfigValidator
{
    public static void Validate(AgentsConfig config)
    {
        if (config.Version != 1)
            throw new ConfigException($"Unsupported config version: {config.Version}. Only version 1 is supported.");

        foreach (var skill in config.Skills)
        {
            if (!SkillDependencyHelpers.IsValidSkillSource(skill.Source))
                throw new ConfigException($"Invalid skill source: '{skill.Source}'.");

            if (skill is RegularSkillDependency reg && !SkillDependencyHelpers.IsValidSkillName(reg.Name))
                throw new ConfigException($"Invalid skill name: '{reg.Name}'. Must match ^[a-zA-Z0-9][a-zA-Z0-9._-]*$.");
        }

        foreach (var mcp in config.Mcp)
        {
            if (string.IsNullOrEmpty(mcp.Name))
                throw new ConfigException("MCP server name must not be empty.");

            var hasStdio = mcp.Command is not null;
            var hasHttp = mcp.Url is not null;

            if (!hasStdio && !hasHttp)
                throw new ConfigException($"MCP server '{mcp.Name}' must specify either 'command' (stdio) or 'url' (http).");

            if (hasStdio && hasHttp)
                throw new ConfigException($"MCP server '{mcp.Name}' must specify 'command' or 'url', not both.");
        }

        // Detect duplicate wildcard sources
        var wildcardSources = config.Skills
            .OfType<WildcardSkillDependency>()
            .GroupBy(w => w.Source)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (wildcardSources.Count > 0)
            throw new ConfigException($"Duplicate wildcard skill sources: {string.Join(", ", wildcardSources)}.");
    }
}
