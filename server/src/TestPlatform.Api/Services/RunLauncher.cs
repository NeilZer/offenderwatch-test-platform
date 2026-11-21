using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TestPlatform.Api.Domain;
using TestPlatform.Api.Hubs;
using TestPlatform.Api.Infrastructure;

namespace TestPlatform.Api.Services;

/// <summary>
/// Starts the Part 3 suite as a child process and owns its lifetime (TM-02).
///
/// Integration model = PUSH: this class only launches and stops the process. Results do NOT come from
/// parsing its output — the suite's Playwright reporter POSTs each scenario to the ingestion API while
/// it runs. The child gets everything it needs as environment variables:
///   BASE_URL          the selected environment (no URL is hard-coded in the suite or the platform)
///   PLATFORM_URL      where to report
///   PLATFORM_RUN_ID   which run to report into
///   PLATFORM_TOKEN    ingestion secret
/// The console output is streamed live to the run page and saved as runner-output.log.
/// </summary>
public partial class RunLauncher(
    IServiceScopeFactory scopes,
    IOptions<PlatformOptions> platform,
    IOptions<SuiteOptions> suite,
    PlatformPaths paths,
    RunEvents events,
    ILogger<RunLauncher> logger)
{
    private readonly ConcurrentDictionary<int, LaunchedRun> running = new();

    private sealed class LaunchedRun(Process process)
    {
        public Process Process { get; } = process;
        public List<string> Output { get; } = new();
        public object Gate { get; } = new();
        public volatile bool StopRequested;
        public string? StoppedBy;
    }

    public bool IsRunning(int runId) => running.ContainsKey(runId);

    public IReadOnlyList<string> CurrentOutput(int runId)
    {
        if (!running.TryGetValue(runId, out var launched)) return Array.Empty<string>();
        lock (launched.Gate) return launched.Output.ToList();
    }

    /// <summary>Validates scope + filter WITHOUT starting anything — invalid input must never create a run.</summary>
    public void ValidateRequest(string scope, string? grep) => BuildArguments(scope, grep);

    public void Launch(Run run, string? grep, bool keepTestData = false)
    {
        if (string.IsNullOrWhiteSpace(paths.SuiteDirectory) || !Directory.Exists(paths.SuiteDirectory))
            throw new ConflictException($"Suite folder not found: '{paths.SuiteDirectory}'. Set Suite:WorkingDirectory in appsettings.json.");

        var args = BuildArguments(run.Scope, grep);
        // cmd.exe gets the raw command line (no extra .NET quoting); every part is from config or a
        // validated filter without spaces or shell characters, so nothing needs quoting.
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", $"/d /c {args}")
            : new ProcessStartInfo("/bin/sh") { ArgumentList = { "-c", args } };
        startInfo.WorkingDirectory = paths.SuiteDirectory;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.Environment["BASE_URL"] = run.EnvironmentBaseUrl;
        startInfo.Environment["PLATFORM_URL"] = platform.Value.PublicUrl;
        startInfo.Environment["PLATFORM_RUN_ID"] = run.Id.ToString();
        startInfo.Environment["PLATFORM_TOKEN"] = platform.Value.IngestToken;
        startInfo.Environment["FORCE_COLOR"] = "0";
        startInfo.Environment["HEADED"] = "false";
        startInfo.Environment["KEEP_TEST_DATA"] = keepTestData ? "true" : "false";

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var launched = new LaunchedRun(process);
        process.OutputDataReceived += (_, e) => OnOutput(run.Id, launched, e.Data);
        process.ErrorDataReceived += (_, e) => OnOutput(run.Id, launched, e.Data);

        if (!process.Start()) throw new InvalidOperationException("The suite process did not start");
        running[run.Id] = launched;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        logger.LogInformation("Run {RunId}: started suite (pid {Pid}): {Args}", run.Id, process.Id, args);

        _ = WatchExitAsync(run.Id, launched);
    }

    /// <summary>
    /// Stops a running suite: kills the whole process tree (cmd → npx → playwright runner → workers → browsers).
    /// On Windows, .NET's Process.Kill(entireProcessTree) proved unreliable for this chain (the Playwright
    /// runner below an intermediate cmd.exe survived), so Windows' own "taskkill /T /F" is used first.
    /// </summary>
    public bool Stop(int runId, string? user)
    {
        if (!running.TryGetValue(runId, out var launched)) return false;
        launched.StopRequested = true;
        launched.StoppedBy = user;
        int pid;
        try { pid = launched.Process.Id; }
        catch (InvalidOperationException) { return true; } // already exited — the exit watcher finalises the run

        if (OperatingSystem.IsWindows()) KillTreeWithTaskkill(pid);
        try
        {
            if (!launched.Process.HasExited) launched.Process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or AggregateException)
        {
            logger.LogDebug(ex, "Run {RunId}: process already gone while stopping", runId);
        }
        logger.LogInformation("Run {RunId}: stop requested by {User}", runId, user);
        return true;
    }

    private void KillTreeWithTaskkill(int pid)
    {
        try
        {
            using var taskkill = Process.Start(new ProcessStartInfo("taskkill.exe")
            {
                ArgumentList = { "/PID", pid.ToString(), "/T", "/F" },
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            taskkill?.WaitForExit(10_000);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "taskkill failed for pid {Pid}", pid);
        }
    }

    private void OnOutput(int runId, LaunchedRun launched, string? line)
    {
        if (line is null) return;
        line = AnsiEscape().Replace(line, "");
        lock (launched.Gate)
        {
            launched.Output.Add(line);
            if (launched.Output.Count > 5000) launched.Output.RemoveRange(0, 1000);
        }
        _ = events.RunOutput(runId, new[] { line });
    }

    private async Task WatchExitAsync(int runId, LaunchedRun launched)
    {
        try
        {
            await launched.Process.WaitForExitAsync();
            var exitCode = SafeExitCode(launched.Process);
            await SaveOutputAsync(runId, launched);

            using var scope = scopes.CreateScope();
            var runs = scope.ServiceProvider.GetRequiredService<RunService>();
            if (launched.StopRequested)
            {
                await runs.FinalizeAsync(runId, RunStatus.Stopped, $"Stopped by {launched.StoppedBy ?? "a user"}", CancellationToken.None);
            }
            else
            {
                // Normally the reporter already ended the run; this covers crashes before/without "end".
                // Exit code 1 only means "some test failed unexpectedly" — the run itself completed.
                var status = exitCode is 0 or 1 ? RunStatus.Completed : RunStatus.Error;
                await runs.FinalizeAsync(runId, status, status == RunStatus.Error ? $"Runner exited with code {exitCode}" : null, CancellationToken.None);
            }
            await scope.ServiceProvider.GetRequiredService<RegressionNotifier>().OnRunFinishedAsync(runId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Run {RunId}: failed while handling process exit", runId);
        }
        finally
        {
            running.TryRemove(runId, out _);
            launched.Process.Dispose();
        }
    }

    private async Task SaveOutputAsync(int runId, LaunchedRun launched)
    {
        List<string> lines;
        lock (launched.Gate) lines = launched.Output.ToList();
        Directory.CreateDirectory(paths.RunDirectory(runId));
        var file = paths.RunnerOutputFile(runId);
        if (File.Exists(file)) return; // write-once
        await File.WriteAllLinesAsync(file, lines);
        File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
    }

    private string BuildArguments(string scope, string? grep)
    {
        var s = suite.Value;
        if (!s.Scopes.TryGetValue(scope, out var scopeArgs)) throw new BadRequestException($"Unknown scope '{scope}'. Use one of: {string.Join(", ", s.Scopes.Keys)}");
        var args = $"{s.Command} {scopeArgs}";
        if (!string.IsNullOrWhiteSpace(grep))
        {
            // The value reaches a shell: allow only characters a test title / tag filter needs.
            if (!SafeGrep().IsMatch(grep)) throw new BadRequestException("Filter: one word of letters, digits and @ - _ . (e.g. TC-47 or @FR-04)");
            args += $" --grep {grep}";
        }
        return args;
    }

    private static int SafeExitCode(Process p)
    {
        try { return p.ExitCode; } catch { return -1; }
    }

    [GeneratedRegex(@"\x1B\[[0-9;]*[A-Za-z]")]
    private static partial Regex AnsiEscape();

    [GeneratedRegex(@"^[A-Za-z0-9@\-_.]{1,80}$")]
    private static partial Regex SafeGrep();
}
