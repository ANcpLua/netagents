namespace NetAgents.Skills;

using System.Text.RegularExpressions;
using Config;
using Sources;

public sealed class ResolveException(string message) : Exception(message);

// ── Resolved skill types ─────────────────────────────────────────────────────

public abstract record ResolvedSkill(string Source, string SkillDir);

public sealed record ResolvedGitSkill(
    string Source,
    string ResolvedUrl,
    string ResolvedPath,
    string? ResolvedRef,
    string Commit,
    string SkillDir) : ResolvedSkill(Source, SkillDir);

public sealed record ResolvedLocalSkill(string Source, string SkillDir) : ResolvedSkill(Source, SkillDir);

public sealed record NamedResolvedSkill(string Name, ResolvedSkill Skill);

// ── Parsed source ────────────────────────────────────────────────────────────

public enum SourceType
{
    Github,
    Git,
    Local
}

public sealed record ParsedSource(
    SourceType Type,
    string? Url = null,
    string? CloneUrl = null,
    string? Owner = null,
    string? Repo = null,
    string? Ref = null,
    string? Path = null);

// ── Resolver ─────────────────────────────────────────────────────────────────

public static partial class SkillResolver
{
    [GeneratedRegex(@"^https?://", RegexOptions.IgnoreCase)]
    private static partial Regex HttpScheme();

    [GeneratedRegex(@"\.git$")]
    private static partial Regex DotGitSuffix();

    public static bool IsExplicitSourceSpecifier(string specifier)
    {
        return specifier.StartsWith("path:", StringComparison.Ordinal) ||
               specifier.StartsWith("git:", StringComparison.Ordinal) ||
               specifier.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               specifier.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               specifier.StartsWith("git@", StringComparison.Ordinal);
    }

    public static (string Owner, string Repo, string? Ref)? ParseOwnerRepoShorthand(string specifier)
    {
        if (IsExplicitSourceSpecifier(specifier))
            return null;

        var atIdx = specifier.IndexOf('@');
        var basePart = atIdx >= 0 ? specifier[..atIdx] : specifier;
        var refPart = atIdx >= 0 ? specifier[(atIdx + 1)..] : null;
        var parts = basePart.Split('/');

        if (parts.Length != 2)
            return null;

        var (owner, repo) = (parts[0], parts[1]);
        if (owner.Length == 0 || repo.Length == 0 || owner.StartsWith('-') || repo.StartsWith('-'))
            return null;

        return (owner, repo, refPart);
    }

    /// <summary>
    ///     Expand owner/repo shorthand according to defaultRepositorySource.
    ///     Returns input unchanged for explicit sources or non-shorthand values.
    /// </summary>
    public static string ApplyDefaultRepositorySource(
        string specifier,
        RepositorySource defaultRepositorySource = RepositorySource.Github)
    {
        if (IsExplicitSourceSpecifier(specifier))
            return specifier;

        var shorthand = ParseOwnerRepoShorthand(specifier);
        if (shorthand is null)
            return specifier;

        var (owner, repo, @ref) = shorthand.Value;
        var host = defaultRepositorySource == RepositorySource.Gitlab ? "gitlab.com" : "github.com";
        var refSuffix = @ref is not null ? $"@{@ref}" : "";
        return $"https://{host}/{owner}/{repo}{refSuffix}";
    }

    /// <summary>
    ///     Parse a source string into its components.
    /// </summary>
    public static ParsedSource ParseSource(string source)
    {
        if (source.StartsWith("path:", StringComparison.Ordinal))
            return new ParsedSource(SourceType.Local, Path: source[5..]);

        if (source.StartsWith("git:", StringComparison.Ordinal))
            return new ParsedSource(SourceType.Git, source[4..]);

        // GitHub HTTPS or SSH URL
        var ghMatch = SourcePatterns.GithubHttpsUrl().Match(source);
        if (!ghMatch.Success) ghMatch = SourcePatterns.GithubSshUrl().Match(source);
        if (ghMatch.Success)
        {
            var (owner, repo, @ref) = ExtractGroups(ghMatch);
            var withoutRef = @ref is not null ? source[..^(@ref.Length + 1)] : source;
            var cloneUrl = HttpScheme().IsMatch(withoutRef)
                ? HttpScheme().Replace(withoutRef, "https://")
                : withoutRef;
            return new ParsedSource(
                SourceType.Github,
                $"https://github.com/{owner}/{repo}.git",
                cloneUrl,
                owner,
                repo,
                @ref);
        }

        // GitLab HTTPS or SSH URL
        var glMatch = SourcePatterns.GitlabHttpsUrl().Match(source);
        if (!glMatch.Success) glMatch = SourcePatterns.GitlabSshUrl().Match(source);
        if (glMatch.Success)
        {
            var (owner, repo, @ref) = ExtractGroups(glMatch);
            var withoutRef = @ref is not null ? source[..^(@ref.Length + 1)] : source;
            var cloneUrl = HttpScheme().IsMatch(withoutRef)
                ? HttpScheme().Replace(withoutRef, "https://")
                : withoutRef;
            return new ParsedSource(
                SourceType.Git,
                $"https://gitlab.com/{owner}/{repo}.git",
                cloneUrl,
                owner,
                repo,
                @ref);
        }

        // owner/repo or owner/repo@ref shorthand -- no cloneUrl
        var atIdx = source.IndexOf('@');
        var basePart = atIdx == -1 ? source : source[..atIdx];
        var refVal = atIdx == -1 ? null : source[(atIdx + 1)..];
        var splitParts = basePart.Split('/');

        return new ParsedSource(
            SourceType.Github,
            $"https://github.com/{splitParts[0]}/{splitParts[1]}.git",
            Owner: splitParts[0],
            Repo: splitParts[1],
            Ref: refVal);
    }

    /// <summary>Normalize hosted sources to canonical owner/repo form for comparison/dedup.</summary>
    public static string NormalizeSource(string source)
    {
        var parsed = ParseSource(source);
        return parsed.Owner is not null && parsed.Repo is not null
            ? $"{parsed.Owner}/{parsed.Repo}"
            : source;
    }

    /// <summary>Compare two source strings for equivalence (normalizes hosted URLs to owner/repo).</summary>
    public static bool SourcesMatch(string a, string b)
    {
        return string.Equals(NormalizeSource(a), NormalizeSource(b), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Resolve a skill dependency to a concrete directory on disk.
    /// </summary>
    public static async Task<ResolvedSkill> ResolveSkillAsync(
        string skillName,
        RegularSkillDependency dep,
        string? projectRoot = null,
        RepositorySource defaultRepositorySource = RepositorySource.Github,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        var sourceForResolve = ApplyDefaultRepositorySource(dep.Source, defaultRepositorySource);
        var parsed = ParseSource(sourceForResolve);

        if (parsed.Type == SourceType.Local)
        {
            var root = projectRoot ?? Directory.GetCurrentDirectory();
            var skillDir = await LocalSource.ResolveLocalSourceAsync(root, parsed.Path!, ct)
                .ConfigureAwait(false);
            return new ResolvedLocalSkill(dep.Source, skillDir);
        }

        // Git source (GitHub or generic git)
        var url = parsed.Url!;
        var cloneUrl = parsed.CloneUrl ?? url;
        var @ref = dep.Ref ?? parsed.Ref;
        var cacheKey = parsed.Type == SourceType.Github
            ? $"{parsed.Owner}/{parsed.Repo}"
            : DotGitSuffix().Replace(HttpScheme().Replace(url, ""), "");

        var cached = await SkillCache.EnsureCachedAsync(cloneUrl, cacheKey, @ref, ttl, ct)
            .ConfigureAwait(false);

        // Discover the skill within the repo
        DiscoveredSkill? discovered;
        if (dep.Path is not null)
        {
            var meta = await SkillLoader.LoadSkillMdAsync(
                Path.Combine(cached.RepoDir, dep.Path, "SKILL.md"), ct).ConfigureAwait(false);
            discovered = new DiscoveredSkill(dep.Path, meta);
        }
        else
        {
            discovered = await SkillDiscovery.DiscoverSkillAsync(cached.RepoDir, skillName, ct)
                .ConfigureAwait(false);
        }

        if (discovered is null)
            throw new ResolveException(
                $"Skill \"{skillName}\" not found in {dep.Source}. " +
                "Tried conventional directories. Use the 'path' field to specify the location explicitly.");

        return new ResolvedGitSkill(
            dep.Source,
            cloneUrl,
            discovered.Path,
            @ref,
            cached.Commit,
            Path.Combine(cached.RepoDir, discovered.Path));
    }

    /// <summary>
    ///     Resolve a wildcard dependency: discover all skills from a source and return them.
    ///     Excludes are filtered out. Skill names are validated to prevent path traversal.
    /// </summary>
    public static async Task<IReadOnlyList<NamedResolvedSkill>> ResolveWildcardSkillsAsync(
        WildcardSkillDependency dep,
        string? projectRoot = null,
        RepositorySource defaultRepositorySource = RepositorySource.Github,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        var sourceForResolve = ApplyDefaultRepositorySource(dep.Source, defaultRepositorySource);
        var parsed = ParseSource(sourceForResolve);
        var excludeSet = new HashSet<string>(dep.Exclude, StringComparer.Ordinal);

        if (parsed.Type == SourceType.Local)
        {
            var root = projectRoot ?? Directory.GetCurrentDirectory();
            var skillDir = await LocalSource.ResolveLocalSourceAsync(root, parsed.Path!, ct)
                .ConfigureAwait(false);
            var discovered = await SkillDiscovery.DiscoverAllSkillsAsync(skillDir, ct)
                .ConfigureAwait(false);
            return discovered
                .Where(d => !excludeSet.Contains(d.Meta.Name) &&
                            SourcePatterns.ValidSkillName().IsMatch(d.Meta.Name))
                .Select(d => new NamedResolvedSkill(
                    d.Meta.Name,
                    new ResolvedLocalSkill(dep.Source, Path.Combine(skillDir, d.Path))))
                .ToList();
        }

        // Git source
        var url = parsed.Url!;
        var cloneUrl = parsed.CloneUrl ?? url;
        var @ref = dep.Ref ?? parsed.Ref;
        var cacheKey = parsed.Type == SourceType.Github
            ? $"{parsed.Owner}/{parsed.Repo}"
            : DotGitSuffix().Replace(HttpScheme().Replace(url, ""), "");

        var cached = await SkillCache.EnsureCachedAsync(cloneUrl, cacheKey, @ref, ttl, ct)
            .ConfigureAwait(false);

        var allSkills = await SkillDiscovery.DiscoverAllSkillsAsync(cached.RepoDir, ct)
            .ConfigureAwait(false);

        return allSkills
            .Where(d => !excludeSet.Contains(d.Meta.Name) &&
                        SourcePatterns.ValidSkillName().IsMatch(d.Meta.Name))
            .Select(d => new NamedResolvedSkill(
                d.Meta.Name,
                new ResolvedGitSkill(
                    dep.Source,
                    cloneUrl,
                    d.Path,
                    @ref,
                    cached.Commit,
                    Path.Combine(cached.RepoDir, d.Path))))
            .ToList();
    }

    private static (string Owner, string Repo, string? Ref) ExtractGroups(Match match)
    {
        return (match.Groups[1].Value,
            match.Groups[2].Value,
            match.Groups[3].Success && match.Groups[3].Value.Length > 0 ? match.Groups[3].Value : null);
    }
}
