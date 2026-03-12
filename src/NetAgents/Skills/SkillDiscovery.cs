namespace NetAgents.Skills;

public sealed record DiscoveredSkill(string Path, SkillMeta Meta);

/// <summary>
/// Scans conventional directories for skills (directories containing SKILL.md).
/// </summary>
public static class SkillDiscovery
{
    /// <summary>
    /// Conventional root dir scanned flat (direct children only).
    /// </summary>
    private const string RootScanDir = ".";

    /// <summary>
    /// Directories scanned recursively to handle categorized layouts.
    /// </summary>
    private static readonly string[] RecursiveScanDirs = ["skills", ".agents/skills", ".claude/skills"];

    private sealed record ScanDir(string Dir, bool Recursive);

    private static readonly ScanDir[] AllScanDirs =
    [
        new(RootScanDir, false),
        ..RecursiveScanDirs.Select(d => new ScanDir(d, true)),
    ];

    private sealed record SkillDir(string AbsPath, string RelPath);

    /// <summary>
    /// Discover a specific skill by name within a repo directory.
    /// Scans conventional directories in priority order, recursing into subdirectories until SKILL.md is found.
    /// </summary>
    public static async Task<DiscoveredSkill?> DiscoverSkillAsync(
        string repoDir,
        string skillName,
        CancellationToken ct = default)
    {
        foreach (var scanDir in AllScanDirs)
        {
            var skillDirs = await ListSkillDirsAsync(repoDir, scanDir.Dir, scanDir.Recursive, ct)
                .ConfigureAwait(false);

            DiscoveredSkill? dirNameMatch = null;
            DiscoveredSkill? frontmatterMatch = null;

            foreach (var sd in skillDirs)
            {
                var dirName = sd.RelPath.Split('/')[^1];
                var fullRelPath = scanDir.Dir == RootScanDir
                    ? sd.RelPath
                    : $"{scanDir.Dir}/{sd.RelPath}";

                try
                {
                    var meta = await SkillLoader.LoadSkillMdAsync(
                        System.IO.Path.Combine(sd.AbsPath, "SKILL.md"), ct).ConfigureAwait(false);

                    if (dirNameMatch is null && dirName == skillName)
                        dirNameMatch = new DiscoveredSkill(fullRelPath, meta);
                    else if (frontmatterMatch is null && meta.Name == skillName)
                        frontmatterMatch = new DiscoveredSkill(fullRelPath, meta);
                }
                catch (SkillLoadException)
                {
                    // Skip skills with invalid SKILL.md
                }
            }

            // Any match in this scan dir wins over later scan dirs
            if (dirNameMatch is not null)
                return dirNameMatch;
            if (frontmatterMatch is not null)
                return frontmatterMatch;
        }

        // Marketplace format
        var marketplaceSkill = await TryMarketplaceFormatAsync(repoDir, skillName, ct)
            .ConfigureAwait(false);
        if (marketplaceSkill is not null)
            return marketplaceSkill;

        return null;
    }

    /// <summary>
    /// Discover all skills in a repo.
    /// Scans conventional directories recursively and returns everything found.
    /// </summary>
    public static async Task<IReadOnlyList<DiscoveredSkill>> DiscoverAllSkillsAsync(
        string repoDir,
        CancellationToken ct = default)
    {
        var found = new Dictionary<string, DiscoveredSkill>(StringComparer.Ordinal);

        foreach (var scanDir in AllScanDirs)
        {
            var skillDirs = await ListSkillDirsAsync(repoDir, scanDir.Dir, scanDir.Recursive, ct)
                .ConfigureAwait(false);

            foreach (var sd in skillDirs)
            {
                try
                {
                    var meta = await SkillLoader.LoadSkillMdAsync(
                        System.IO.Path.Combine(sd.AbsPath, "SKILL.md"), ct).ConfigureAwait(false);

                    // First match wins (higher priority scan dirs are checked first)
                    if (found.ContainsKey(meta.Name))
                        continue;

                    var fullRelPath = scanDir.Dir == RootScanDir
                        ? sd.RelPath
                        : $"{scanDir.Dir}/{sd.RelPath}";
                    found[meta.Name] = new DiscoveredSkill(fullRelPath, meta);
                }
                catch (SkillLoadException)
                {
                    // Skip skills with invalid SKILL.md
                }
            }
        }

        // Marketplace format: plugins/*/skills/*/SKILL.md
        var marketplaceSkills = await ScanMarketplaceFormatAsync(repoDir, ct).ConfigureAwait(false);
        foreach (var skill in marketplaceSkills)
        {
            found.TryAdd(skill.Meta.Name, skill);
        }

        return [.. found.Values];
    }

    /// <summary>
    /// List skill directories within a scan dir.
    /// Root dir is scanned flat; other dirs are walked recursively.
    /// </summary>
    private static async Task<List<SkillDir>> ListSkillDirsAsync(
        string repoDir,
        string scanDir,
        bool recursive,
        CancellationToken ct)
    {
        var absDir = System.IO.Path.Combine(repoDir, scanDir);

        if (recursive)
            return await WalkSkillDirsAsync(absDir, "", ct).ConfigureAwait(false);

        // Flat scan: only direct children with SKILL.md
        if (!Directory.Exists(absDir))
            return [];

        DirectoryInfo dirInfo;
        try
        {
            dirInfo = new DirectoryInfo(absDir);
        }
        catch
        {
            return [];
        }

        var results = new List<SkillDir>();
        foreach (var subDir in dirInfo.EnumerateDirectories())
        {
            ct.ThrowIfCancellationRequested();
            if (File.Exists(System.IO.Path.Combine(subDir.FullName, "SKILL.md")))
                results.Add(new SkillDir(subDir.FullName, subDir.Name));
        }

        return results;
    }

    /// <summary>
    /// Recursively walk a directory tree finding all directories that contain SKILL.md.
    /// Stops descending into a directory once SKILL.md is found (skill dirs are leaf nodes).
    /// </summary>
    private static async Task<List<SkillDir>> WalkSkillDirsAsync(
        string baseDir,
        string relPrefix,
        CancellationToken ct)
    {
        if (!Directory.Exists(baseDir))
            return [];

        DirectoryInfo dirInfo;
        try
        {
            dirInfo = new DirectoryInfo(baseDir);
        }
        catch
        {
            return [];
        }

        var direct = new List<SkillDir>();
        var nested = new List<SkillDir>();

        foreach (var subDir in dirInfo.EnumerateDirectories())
        {
            ct.ThrowIfCancellationRequested();
            var absPath = subDir.FullName;
            var relPath = relPrefix.Length > 0
                ? $"{relPrefix}/{subDir.Name}"
                : subDir.Name;

            if (File.Exists(System.IO.Path.Combine(absPath, "SKILL.md")))
            {
                // This is a skill directory -- collect it and don't descend further
                direct.Add(new SkillDir(absPath, relPath));
            }
            else
            {
                // Not a skill -- recurse into it to find nested skills
                var children = await WalkSkillDirsAsync(absPath, relPath, ct).ConfigureAwait(false);
                nested.AddRange(children);
            }
        }

        // Direct children first, then nested -- shallower matches have priority
        direct.AddRange(nested);
        return direct;
    }

    private static async Task<IReadOnlyList<DiscoveredSkill>> ScanMarketplaceFormatAsync(
        string repoDir,
        CancellationToken ct)
    {
        var pluginMarkerDir = System.IO.Path.Combine(repoDir, ".claude-plugin");
        if (!Directory.Exists(pluginMarkerDir))
            return [];

        var pluginsDirPath = System.IO.Path.Combine(repoDir, "plugins");
        if (!Directory.Exists(pluginsDirPath))
            return [];

        DirectoryInfo pluginsInfo;
        try
        {
            pluginsInfo = new DirectoryInfo(pluginsDirPath);
        }
        catch
        {
            return [];
        }

        var results = new List<DiscoveredSkill>();

        foreach (var plugin in pluginsInfo.EnumerateDirectories())
        {
            ct.ThrowIfCancellationRequested();
            var skillsDir = System.IO.Path.Combine(plugin.FullName, "skills");
            if (!Directory.Exists(skillsDir))
                continue;

            DirectoryInfo skillsInfo;
            try
            {
                skillsInfo = new DirectoryInfo(skillsDir);
            }
            catch
            {
                continue;
            }

            foreach (var entry in skillsInfo.EnumerateDirectories())
            {
                ct.ThrowIfCancellationRequested();
                var skillMdPath = System.IO.Path.Combine(entry.FullName, "SKILL.md");
                if (!File.Exists(skillMdPath))
                    continue;

                try
                {
                    var meta = await SkillLoader.LoadSkillMdAsync(skillMdPath, ct).ConfigureAwait(false);
                    results.Add(new DiscoveredSkill(
                        $"plugins/{plugin.Name}/skills/{entry.Name}",
                        meta));
                }
                catch (SkillLoadException)
                {
                    // Skip invalid
                }
            }
        }

        return results;
    }

    private static async Task<DiscoveredSkill?> TryMarketplaceFormatAsync(
        string repoDir,
        string skillName,
        CancellationToken ct)
    {
        var pluginMarkerDir = System.IO.Path.Combine(repoDir, ".claude-plugin");
        if (!Directory.Exists(pluginMarkerDir))
            return null;

        var pluginsDirPath = System.IO.Path.Combine(repoDir, "plugins");
        if (!Directory.Exists(pluginsDirPath))
            return null;

        DirectoryInfo pluginsInfo;
        try
        {
            pluginsInfo = new DirectoryInfo(pluginsDirPath);
        }
        catch
        {
            return null;
        }

        foreach (var plugin in pluginsInfo.EnumerateDirectories())
        {
            ct.ThrowIfCancellationRequested();
            var skillMdPath = System.IO.Path.Combine(
                plugin.FullName, "skills", skillName, "SKILL.md");
            if (!File.Exists(skillMdPath))
                continue;

            try
            {
                var meta = await SkillLoader.LoadSkillMdAsync(skillMdPath, ct).ConfigureAwait(false);
                return new DiscoveredSkill(
                    $"plugins/{plugin.Name}/skills/{skillName}",
                    meta);
            }
            catch (SkillLoadException)
            {
                // Skip skills with invalid SKILL.md
            }
        }

        return null;
    }
}
