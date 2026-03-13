namespace NetAgents.Utils;

using System.Collections;
using System.Diagnostics;
using System.Text;

public record ProcessResult(string Stdout, string Stderr);

public sealed class ProcessRunnerException(string message, int? exitCode, string stderr)
    : Exception(message)
{
    public int? ExitCode { get; } = exitCode;
    public string Stderr { get; } = stderr;
}

public static class ProcessRunner
{
    private const int MaxBufferBytes = 50 * 1024 * 1024;

    public static async Task<ProcessResult> RunAsync(
        string command,
        string[] args,
        string? cwd = null,
        Dictionary<string, string>? env = null,
        int timeoutMs = 60_000,
        CancellationToken ct = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        if (cwd is not null)
            startInfo.WorkingDirectory = cwd;

        // Inherit current environment, then apply git safety defaults, then user overrides
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            startInfo.Environment[(string)entry.Key] = (string?)entry.Value ?? string.Empty;

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_SSH_COMMAND"] = "ssh -o BatchMode=yes";

        if (env is not null)
            foreach (var (key, value) in env)
                startInfo.Environment[key] = value;

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var stdoutTask = ReadStreamAsync(process.StandardOutput, stdoutBuilder, MaxBufferBytes, cts.Token);
        var stderrTask = ReadStreamAsync(process.StandardError, stderrBuilder, MaxBufferBytes, cts.Token);

        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout fired, not external cancellation
            try
            {
                process.Kill(true);
            }
            catch
            {
                /* best effort */
            }

            var timeoutStderr = stderrBuilder.ToString();
            throw new ProcessRunnerException(
                $"{command} {string.Join(" ", args)} timed out after {timeoutMs}ms",
                null,
                timeoutStderr);
        }

        var stdout = stdoutBuilder.ToString();
        var stderr = stderrBuilder.ToString();

        if (process.ExitCode != 0)
            throw new ProcessRunnerException(
                $"{command} {string.Join(" ", args)} failed: {(stderr.Trim() is { Length: > 0 } msg ? msg : $"exit code {process.ExitCode}")}",
                process.ExitCode,
                stderr);

        return new ProcessResult(stdout, stderr);
    }

    private static async Task ReadStreamAsync(
        StreamReader reader,
        StringBuilder builder,
        int maxBytes,
        CancellationToken ct)
    {
        var buffer = new char[4096];
        int bytesRead;
        while ((bytesRead = await reader.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            if (builder.Length + bytesRead > maxBytes)
                throw new InvalidOperationException(
                    $"Process output exceeded maximum buffer size of {maxBytes} bytes.");
            builder.Append(buffer, 0, bytesRead);
        }
    }
}
