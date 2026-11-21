using Microsoft.EntityFrameworkCore;
using TestPlatform.Api.Data;
using TestPlatform.Api.Domain;
using TestPlatform.Api.Infrastructure;

namespace TestPlatform.Api.Services;

/// <summary>Start / stop rules shared by the UI, the API and the scheduler.</summary>
/// <summary>Everything needed to start a run, whoever starts it (UI, API, scheduler).</summary>
public sealed record StartRunRequest(
    int EnvironmentId,
    string? Scope,
    string? Filter,
    RunTrigger Trigger,
    string? StartedBy,
    int? ScheduleId = null,
    bool KeepTestData = false);

public class RunOrchestrator(PlatformDbContext db, RunService runs, RunLauncher launcher, ILogger<RunOrchestrator> logger)
{
    /// <summary>
    /// Serialises "is an environment free? → create the run". Without it, a double-click or the
    /// scheduler firing together with a user could start two runs on the same environment.
    /// (One API process owns the database, so an in-process lock is sufficient.)
    /// </summary>
    private static readonly SemaphoreSlim StartLock = new(1, 1);

    /// <summary>
    /// Starts a run. One active run per environment: the suite shares one live instance and asserts
    /// exact counter changes, so two runs on the same environment would corrupt each other's results.
    /// </summary>
    public async Task<Run> StartAsync(StartRunRequest request, CancellationToken ct)
    {
        var scope = string.IsNullOrWhiteSpace(request.Scope) ? "all" : request.Scope.Trim().ToLowerInvariant();
        launcher.ValidateRequest(scope, request.Filter); // 400 before anything is recorded

        Run run;
        await StartLock.WaitAsync(ct);
        try
        {
            var env = await db.Environments.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.EnvironmentId, ct)
                      ?? throw new NotFoundException($"Environment {request.EnvironmentId} not found");
            await EnsureEnvironmentIsFreeAsync(env, ct);
            run = await runs.CreateRunAsync(env, request.Trigger, scope, request.StartedBy, request.ScheduleId, ct, request.Filter);
        }
        finally
        {
            StartLock.Release();
        }

        try
        {
            launcher.Launch(run, request.Filter, request.KeepTestData);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Run {RunId} could not start", run.Id);
            await runs.FinalizeAsync(run.Id, RunStatus.Error, $"Could not start the suite: {ex.Message}", ct);
            throw;
        }
        return run;
    }

    private async Task EnsureEnvironmentIsFreeAsync(TargetEnvironment env, CancellationToken ct)
    {
        var active = await db.Runs.AsNoTracking()
            .Where(r => r.EnvironmentId == env.Id && r.FinishedAt == null)
            .Select(r => r.Id).FirstOrDefaultAsync(ct);
        if (active != 0) throw new ConflictException($"Run {active} is still active on {env.Name}. Stop it or wait for it to finish.");
    }

    public async Task StopAsync(int runId, string? user, CancellationToken ct)
    {
        var run = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct) ?? throw new NotFoundException($"Run {runId} not found");
        if (run.FinishedAt is not null) throw new ConflictException($"Run {runId} already finished ({run.Status})");

        // Platform-launched: kill the process; the exit watcher finalises the run as Stopped.
        if (launcher.Stop(runId, user)) return;

        // Started elsewhere (CLI) or the process is already gone: close the run record directly.
        await runs.FinalizeAsync(runId, RunStatus.Stopped, $"Stopped by {user ?? "a user"} (no platform-managed process)", ct);
    }

    /// <summary>Runs that were open when the platform last shut down cannot be resumed — close them honestly.</summary>
    public async Task RecoverInterruptedRunsAsync(CancellationToken ct)
    {
        var open = await db.Runs.Where(r => r.FinishedAt == null).Select(r => r.Id).ToListAsync(ct);
        foreach (var id in open)
        {
            await runs.FinalizeAsync(id, RunStatus.Error, "The platform restarted while this run was in progress.", ct);
        }
        if (open.Count > 0) logger.LogWarning("Closed {Count} run(s) left open by a previous shutdown", open.Count);
    }
}
