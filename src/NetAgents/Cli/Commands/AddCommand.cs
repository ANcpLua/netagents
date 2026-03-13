namespace NetAgents.Cli.Commands;

using Config;
using Skills;
using Sources;
using Trust;

public sealed class AddException(string message) : Exception(message);

public sealed class AddCancelledException() : Exception("Cancelled");

public sealed record AddOptions(
    ScopeRoot Scope,
    string Specifier,
    string? Ref = null,
    IReadOnlyList<string>? Names = null,
    bool All = false,
    bool Interactive = false);

/// <summary>
///     Result is either a single skill name (string), a list of names, or "*" for wildcard.
/// </summary>
public sealed record AddResult(
    string? SingleName,
    IReadOnlyList<string>? MultipleNames,
    bool IsWildcard,
    IReadOnlyList<string> SkippedDuplicates)
{
    public static AddResult Wildcard()
    {
        return new AddResult(null, null, true, []);
    }

    public static AddResult Single(string name)
    {
        return new AddResult(name, null, false, []);
    }

    public static AddResult Multiple(IReadOnlyList<string> names, IReadOnlyList<string> skipped)
    {
        return new AddResult(null, names, false, skipped);
    }
}

public static class AddCommand
{
    public static async Task<AddResult> RunAddAsync(AddOptions opts, CancellationToken ct = default)
    {
        var (scope, specifier, @ref, rawNames, all, interactive) = (
            opts.Scope, opts.Specifier, opts.Ref, opts.Names, opts.All, opts.Interactive);
        var namesOverride = rawNames is not null ? rawNames.Distinct().ToList() : null;

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, ct).ConfigureAwait(false);

        // Validate source format
        if (!SkillResolver.IsExplicitSourceSpecifier(specifier) &&
            SkillResolver.ParseOwnerRepoShorthand(specifier) is null)
            throw new AddException(
                $"Invalid source \"{specifier}\". " +
                "Use owner/repo shorthand, an explicit URL, git:<url>, or path:<relative>.");

        var hintedSpecifier = SkillResolver.ApplyDefaultRepositorySource(
            specifier, config.DefaultRepositorySource);
        var parsed = SkillResolver.ParseSource(hintedSpecifier);

        // Store original source form, strip inline @ref
        var sourceForStorage = parsed.Ref is not null
            ? specifier[..^(parsed.Ref.Length + 1)]
            : specifier;

        TrustValidator.ValidateTrustedSource(hintedSpecifier, config.Trust);

        var effectiveRef = @ref ?? parsed.Ref;

        async Task<AddResult> AddWildcardAsync()
        {
            if (config.Skills.OfType<WildcardSkillDependency>()
                .Any(s => SkillResolver.SourcesMatch(s.Source, sourceForStorage)))
                throw new AddException(
                    $"A wildcard entry for \"{sourceForStorage}\" already exists in agents.toml.");

            await ConfigWriter.AddWildcardToConfigAsync(scope.ConfigPath, sourceForStorage, effectiveRef, [], ct)
                .ConfigureAwait(false);
            await InstallCommand.RunInstallAsync(new InstallOptions(scope), ct).ConfigureAwait(false);
            return AddResult.Wildcard();
        }

        // --all: add a wildcard entry
        if (all)
        {
            if (namesOverride is { Count: > 0 })
                throw new AddException("Cannot use --all with --name. Use one or the other.");
            return await AddWildcardAsync().ConfigureAwait(false);
        }

        // Validate user-provided skill names
        if (namesOverride is { Count: > 0 })
            foreach (var name in namesOverride)
                if (!SourcePatterns.ValidSkillName().IsMatch(name))
                    throw new AddException(
                        $"Invalid skill name \"{name}\". Names must start with alphanumeric and contain only alphanumeric, dots, underscores, or hyphens.");

        string skillName;

        if (parsed.Type == SourceType.Local)
        {
            var localDir = await LocalSource.ResolveLocalSourceAsync(scope.Root, parsed.Path!, ct)
                .ConfigureAwait(false);

            if (namesOverride is { Count: > 0 })
            {
                foreach (var name in namesOverride)
                {
                    var found = await SkillDiscovery.DiscoverSkillAsync(localDir, name, ct).ConfigureAwait(false);
                    if (found is null)
                        throw new AddException(
                            $"Skill \"{name}\" not found in {sourceForStorage}. " +
                            $"Use 'netagents add {sourceForStorage}' without --name to see available skills.");
                }

                if (namesOverride.Count == 1)
                    skillName = namesOverride[0];
                else
                    return await AddMultipleAsync(config, scope, namesOverride, sourceForStorage, effectiveRef, ct)
                        .ConfigureAwait(false);
            }
            else
            {
                var meta = await SkillLoader.LoadSkillMdAsync(Path.Combine(localDir, "SKILL.md"), ct)
                    .ConfigureAwait(false);
                skillName = meta.Name;
            }
        }
        else
        {
            // Git source
            var url = parsed.Url!;
            var cloneUrl = parsed.CloneUrl ?? url;
            var cacheKey = parsed.Type == SourceType.Github
                ? $"{parsed.Owner}/{parsed.Repo}"
                : url.Replace("https://", "").Replace("http://", "").TrimEnd('/');

            var cached = await SkillCache.EnsureCachedAsync(cloneUrl, cacheKey, effectiveRef, ct: ct)
                .ConfigureAwait(false);

            if (namesOverride is { Count: > 0 })
            {
                foreach (var name in namesOverride)
                {
                    var found = await SkillDiscovery.DiscoverSkillAsync(cached.RepoDir, name, ct)
                        .ConfigureAwait(false);
                    if (found is null)
                        throw new AddException(
                            $"Skill \"{name}\" not found in {sourceForStorage}. " +
                            $"Use 'netagents add {sourceForStorage}' without --name to see available skills.");
                }

                if (namesOverride.Count == 1)
                    skillName = namesOverride[0];
                else
                    return await AddMultipleAsync(config, scope, namesOverride, sourceForStorage, effectiveRef, ct)
                        .ConfigureAwait(false);
            }
            else
            {
                var skills = await SkillDiscovery.DiscoverAllSkillsAsync(cached.RepoDir, ct)
                    .ConfigureAwait(false);
                if (skills.Count == 0)
                    throw new AddException($"No skills found in {sourceForStorage}.");

                if (skills.Count == 1)
                {
                    skillName = skills[0].Meta.Name;
                }
                else
                {
                    // Non-interactive: list and ask user to re-run
                    var names = skills.Select(s => s.Meta.Name).Order().ToList();
                    throw new AddException(
                        $"Multiple skills found in {sourceForStorage}: {string.Join(", ", names)}. " +
                        "Specify skill names as arguments, use --skill to specify which ones, or --all for all skills.");
                }
            }
        }

        // Single skill - check if already exists
        if (config.Skills.OfType<RegularSkillDependency>().Any(s => s.Name == skillName))
            throw new AddException(
                $"Skill \"{skillName}\" already exists in agents.toml. Remove it first to re-add.");

        await ConfigWriter.AddSkillToConfigAsync(scope.ConfigPath, skillName, sourceForStorage, effectiveRef, ct: ct)
            .ConfigureAwait(false);
        await InstallCommand.RunInstallAsync(new InstallOptions(scope), ct).ConfigureAwait(false);
        return AddResult.Single(skillName);
    }

    private static async Task<AddResult> AddMultipleAsync(
        AgentsConfig config,
        ScopeRoot scope,
        IReadOnlyList<string> names,
        string sourceForStorage,
        string? effectiveRef,
        CancellationToken ct)
    {
        var toAdd = new List<string>();
        var skipped = new List<string>();
        foreach (var name in names)
            if (config.Skills.OfType<RegularSkillDependency>().Any(s => s.Name == name))
                skipped.Add(name);
            else
                toAdd.Add(name);

        if (toAdd.Count == 0)
            throw new AddException("All specified skills already exist in agents.toml.");

        foreach (var name in toAdd)
            await ConfigWriter.AddSkillToConfigAsync(scope.ConfigPath, name, sourceForStorage, effectiveRef, ct: ct)
                .ConfigureAwait(false);

        await InstallCommand.RunInstallAsync(new InstallOptions(scope), ct).ConfigureAwait(false);
        return AddResult.Multiple(toAdd, skipped);
    }

    public static async Task<int> ExecuteAsync(string[] args, bool isUser, CancellationToken ct = default)
    {
        // Parse args manually
        var positionals = new List<string>();
        var flagNames = new List<string>();
        string? refValue = null;
        var allFlag = false;
        var i = 0;

        while (i < args.Length)
        {
            switch (args[i])
            {
                case "--ref" when i + 1 < args.Length:
                    refValue = args[++i];
                    break;
                case "--name" or "--skill" when i + 1 < args.Length:
                    flagNames.Add(args[++i]);
                    break;
                case "--all":
                    allFlag = true;
                    break;
                default:
                    if (!args[i].StartsWith('-'))
                        positionals.Add(args[i]);
                    break;
            }

            i++;
        }

        var specifier = positionals.Count > 0 ? positionals[0] : null;
        if (specifier is null)
        {
            Console.Error.WriteLine(
                "Usage: netagents add <specifier> [<skill>...] [--skill <name>...] [--ref <ref>] [--all]");
            return 1;
        }

        var positionalNames = positionals.Skip(1).ToList();
        if (positionalNames.Count > 0 && flagNames.Count > 0)
        {
            Console.Error.WriteLine(
                "Cannot mix positional skill names with --skill/--name flags. Use one or the other.");
            return 1;
        }

        var rawNames = positionalNames.Concat(flagNames).ToList();
        var names = rawNames.Count > 0 ? rawNames.Distinct().ToList() : null;

        try
        {
            var scope = isUser
                ? ScopeResolver.ResolveScope(ScopeKind.User)
                : ScopeResolver.ResolveDefaultScope(Path.GetFullPath("."));
            await EnsureUserScope.EnsureUserScopeBootstrappedAsync(scope, ct).ConfigureAwait(false);

            var result = await RunAddAsync(new AddOptions(scope, specifier, refValue, names, allFlag), ct)
                .ConfigureAwait(false);

            foreach (var dup in result.SkippedDuplicates)
                Console.Error.WriteLine($"Skipping \"{dup}\": already exists in agents.toml");

            if (result.IsWildcard)
                Console.WriteLine($"Added all skills from {specifier}");
            else if (result.MultipleNames is not null)
                Console.WriteLine($"Added skills: {string.Join(", ", result.MultipleNames)}");
            else
                Console.WriteLine($"Added skill: {result.SingleName}");

            return 0;
        }
        catch (AddCancelledException)
        {
            return 0;
        }
        catch (Exception ex) when (ex is ScopeException or AddException or TrustException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
