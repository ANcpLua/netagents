using Xunit;

namespace NetAgents.Tests.Cli;

/// <summary>
/// Tests that mutate the process-global NETAGENTS_STATE_DIR environment variable
/// must run sequentially to avoid race conditions where one test's TempDir cleanup
/// deletes cached git repos that another test is still using.
/// </summary>
[CollectionDefinition("SerialGit", DisableParallelization = true)]
public sealed class SerialGitCollection;
