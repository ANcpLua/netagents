namespace NetAgents.Trust;

using System.Text.RegularExpressions;
using Config;

public sealed class TrustException(string message) : Exception(message);

public static partial class TrustValidator
{
    [GeneratedRegex(@"^[a-z]+@([^:]+):")]
    private static partial Regex ScpPattern();

    /// <summary>
    ///     Extract domain from a git URL.
    ///     Supports https://, ssh://, git://, scp-style (git@host:...), file:// (no domain).
    /// </summary>
    public static string? ExtractDomain(string url)
    {
        // git@host.com:owner/repo.git
        var scpMatch = ScpPattern().Match(url);
        if (scpMatch.Success)
            return scpMatch.Groups[1].Value;

        // https://host.com/..., ssh://host.com/..., git://host.com/...
        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed) && !string.IsNullOrEmpty(parsed.Host))
            return parsed.Host;

        return null;
    }

    /// <summary>
    ///     Validate that a source specifier is allowed by the trust configuration.
    ///     No trust config = allow all. allow_all = true = allow all.
    ///     Local path: sources always allowed. Otherwise must match org, repo, or domain.
    /// </summary>
    public static void ValidateTrustedSource(string source, TrustConfig? trust)
    {
        // No trust config -> allow everything
        if (trust is null)
            return;

        // Explicit opt-out
        if (trust.AllowAll)
            return;

        var parsed = ParseSource(source);

        // Local sources are always allowed
        if (parsed.Type == SourceType.Local)
            return;

        if (parsed.Type == SourceType.Github)
        {
            var owner = parsed.Owner!.ToLowerInvariant();
            var repo = $"{owner}/{parsed.Repo!.ToLowerInvariant()}";

            if (trust.GithubOrgs.Any(o => string.Equals(o, owner, StringComparison.OrdinalIgnoreCase)))
                return;

            if (trust.GithubRepos.Any(r => string.Equals(r, repo, StringComparison.OrdinalIgnoreCase)))
                return;

            throw new TrustException(
                $"""
                 Source "{source}" is not trusted. Allowed sources: {FormatAllowed(trust)}.
                 Run: netagents trust add {parsed.Owner!} (or `netagents trust add {parsed.Owner!}/{parsed.Repo!}` for just this repo)
                 """);
        }

        if (parsed.Type == SourceType.Git)
        {
            var domain = ExtractDomain(parsed.Url!)?.ToLowerInvariant();
            if (domain is not null &&
                trust.GitDomains.Any(d => string.Equals(d, domain, StringComparison.OrdinalIgnoreCase)))
                return;

            var hint = domain is not null ? $"\nRun: netagents trust add {domain}" : "";
            throw new TrustException(
                $"""Source "{source}" is not trusted. Allowed sources: {FormatAllowed(trust)}.{hint}""");
        }
    }

    private static string FormatAllowed(TrustConfig trust)
    {
        var parts = new List<string>();
        if (trust.GithubOrgs.Count > 0)
            parts.Add($"orgs: {string.Join(", ", trust.GithubOrgs)}");
        if (trust.GithubRepos.Count > 0)
            parts.Add($"repos: {string.Join(", ", trust.GithubRepos)}");
        if (trust.GitDomains.Count > 0)
            parts.Add($"domains: {string.Join(", ", trust.GitDomains)}");
        return parts.Count > 0 ? string.Join("; ", parts) : "none";
    }

    private static ParsedSource ParseSource(string source)
    {
        if (source.StartsWith("path:", StringComparison.Ordinal))
            return new ParsedSource(SourceType.Local, null, null, null);

        if (source.StartsWith("git:", StringComparison.Ordinal))
            return new ParsedSource(SourceType.Git, null, null, source[4..]);

        // GitHub/GitLab HTTPS or SSH URL patterns
        var githubMatch = SourcePatterns.GithubHttpsUrl().Match(source)
            is { Success: true } gm
            ? gm
            : SourcePatterns.GithubSshUrl().Match(source);
        if (githubMatch.Success)
            return new ParsedSource(SourceType.Github, githubMatch.Groups[1].Value, githubMatch.Groups[2].Value, null);

        // GitLab URLs are treated as generic git sources (same as TS)
        var gitlabMatch = SourcePatterns.GitlabHttpsUrl().Match(source)
            is { Success: true } glm
            ? glm
            : SourcePatterns.GitlabSshUrl().Match(source);
        if (gitlabMatch.Success)
            return new ParsedSource(SourceType.Git, gitlabMatch.Groups[1].Value, gitlabMatch.Groups[2].Value,
                $"https://gitlab.com/{gitlabMatch.Groups[1].Value}/{gitlabMatch.Groups[2].Value}.git");

        // owner/repo or owner/repo@ref shorthand -> github
        var atIdx = source.IndexOf('@');
        var basePart = atIdx >= 0 ? source[..atIdx] : source;
        var parts = basePart.Split('/');
        if (parts.Length == 2)
            return new ParsedSource(SourceType.Github, parts[0], parts[1], null);

        return new ParsedSource(SourceType.Git, null, null, source);
    }

    // ── Source parsing (mirrors dotagents parseSource for trust checks) ───────

    private enum SourceType
    {
        Github,
        Git,
        Local
    }

    private sealed record ParsedSource(SourceType Type, string? Owner, string? Repo, string? Url);
}
