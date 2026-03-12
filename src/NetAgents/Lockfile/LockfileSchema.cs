namespace NetAgents.Lockfile;

public abstract record LockedSkill(string Source);

public sealed record LockedGitSkill(
    string Source,
    string ResolvedUrl,
    string ResolvedPath,
    string? ResolvedRef) : LockedSkill(Source);

public sealed record LockedLocalSkill(string Source) : LockedSkill(Source);

public sealed record LockfileData(int Version, Dictionary<string, LockedSkill> Skills);
