using Microsoft.EntityFrameworkCore;
using TestPlatform.Api.Contracts;
using TestPlatform.Api.Data;
using TestPlatform.Api.Domain;
using TestPlatform.Api.Hubs;
using TestPlatform.Api.Infrastructure;

namespace TestPlatform.Api.Services;

/// <summary>
/// Everything that changes a run: creation, the runner's ingestion calls, and finalisation.
/// Every change is saved first and then broadcast, so the live view never shows something the
/// database does not contain.
/// </summary>
public class RunService(PlatformDbContext db, RunEvents events, EvidenceStore evidenceStore, ILogger<RunService> logger)
{
    // ------------------------------------------------------------------ creation
    public async Task<Run> CreateRunAsync(TargetEnvironment env, RunTrigger trigger, string scope, string? startedBy,
        int? scheduleId, CancellationToken ct, string? filter = null)
    {
        var run = new Run
        {
            EnvironmentId = env.Id,
            EnvironmentName = env.Name,
            EnvironmentBaseUrl = env.BaseUrl,
            Trigger = trigger,
            Status = RunStatus.Queued,
            Scope = scope,
            Filter = string.IsNullOrWhiteSpace(filter) ? null : filter.Trim(),
            StartedBy = startedBy,
            ScheduleId = scheduleId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Runs.Add(run);
        await db.SaveChangesAsync(ct);
        await events.RunUpdated(run.ToSummary());
        return run;
    }

    /// <summary>A runner started outside the platform (CLI) announces itself; the environment is matched by base URL.</summary>
    public async Task<Run> CreateRunForIngestAsync(IngestCreateRunInput input, CancellationToken ct)
    {
        var baseUrl = NormaliseUrl(input.EnvironmentBaseUrl ?? throw new BadRequestException("environmentBaseUrl is required"));
        var envs = await db.Environments.AsNoTracking().ToListAsync(ct);
        var env = envs.FirstOrDefault(e => NormaliseUrl(e.BaseUrl) == baseUrl)
                  ?? throw new NotFoundException($"No environment with base URL {baseUrl}. Create it in the platform first.");
        var trigger = Enum.TryParse<RunTrigger>(input.Trigger, true, out var t) ? t : RunTrigger.Cli;
        return await CreateRunAsync(env, trigger, input.Scope ?? "all", "runner", null, ct, input.Filter);
    }

    // ------------------------------------------------------------------ ingestion
    public async Task<IngestBeginResult> BeginAsync(int runId, IngestBeginInput input, CancellationToken ct)
    {
        var run = await GetOpenRunAsync(runId, ct);
        if (await db.ScenarioResults.AnyAsync(r => r.RunId == runId, ct))
            throw new ConflictException($"Run {runId} has already begun");
        if (input.Scenarios.Count == 0) throw new BadRequestException("A run needs at least one scenario");

        var keys = input.Scenarios.Select(s => s.Key).ToList();
        var existing = await db.TestCases.Where(t => keys.Contains(t.Key)).ToDictionaryAsync(t => t.Key, ct);
        var now = DateTimeOffset.UtcNow;
        var ordinal = 0;
        var results = new List<ScenarioResult>();
        foreach (var s in input.Scenarios)
        {
            if (!existing.TryGetValue(s.Key, out var testCase))
            {
                testCase = new TestCase { Key = s.Key, FirstSeenAt = now };
                db.TestCases.Add(testCase);
                existing[s.Key] = testCase;
            }
            // Descriptive metadata follows the latest code; results (history) are never touched.
            testCase.Title = s.Title;
            testCase.File = s.File ?? "";
            testCase.Project = s.Project ?? "";
            testCase.Tags = string.Join(' ', s.Tags ?? Array.Empty<string>());
            var result = new ScenarioResult { Run = run, TestCase = testCase, Ordinal = ++ordinal, Status = ScenarioStatus.Queued };
            db.ScenarioResults.Add(result);
            results.Add(result);
        }

        run.Status = RunStatus.Running;
        run.StartedAt ??= now;
        run.Total = results.Count;
        run.SuiteVersion = input.SuiteVersion ?? run.SuiteVersion;
        await db.SaveChangesAsync(ct);
        await events.RunUpdated(run.ToSummary());
        logger.LogInformation("Run {RunId} began with {Count} scenarios", runId, results.Count);
        return new IngestBeginResult(runId, results.ToDictionary(r => r.TestCase!.Key, r => r.Id));
    }

    public async Task StartScenarioAsync(int runId, int resultId, CancellationToken ct)
    {
        var result = await GetOpenResultAsync(runId, resultId, ct);
        result.Status = ScenarioStatus.Running;
        result.StartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await BroadcastScenarioAsync(resultId, ct);
    }

    public async Task<Evidence> AddEvidenceAsync(int runId, int resultId, string? kind, IFormFile file, CancellationToken ct)
    {
        // Evidence is attached BEFORE a scenario is finished; a final result never gains new evidence later.
        var result = await GetOpenResultAsync(runId, resultId, ct);
        await using var stream = file.OpenReadStream();
        var evidence = await evidenceStore.SaveAsync(runId, result.Id, EvidenceStore.ParseKind(kind), file.FileName, file.ContentType, stream, ct);
        db.Evidence.Add(evidence);
        await db.SaveChangesAsync(ct);
        return evidence;
    }

    public async Task FinishScenarioAsync(int runId, int resultId, IngestFinishInput input, CancellationToken ct)
    {
        var status = Enum.TryParse<ScenarioStatus>(input.Status, true, out var parsed) && HistoryAnalyzer.IsFinal(parsed)
            ? parsed
            : throw new BadRequestException($"'{input.Status}' is not a final scenario status");
        var result = await GetOpenResultAsync(runId, resultId, ct);
        var now = DateTimeOffset.UtcNow;
        result.Status = status;
        result.StartedAt ??= now;
        result.FinishedAt = now;
        result.DurationMs = input.DurationMs ?? (long)(now - result.StartedAt.Value).TotalMilliseconds;
        result.ErrorMessage = Truncate(input.ErrorMessage, 8_000);
        result.ErrorStack = Truncate(input.ErrorStack, 20_000);
        result.KnownBugs = input.KnownBugs is { Count: > 0 } ? string.Join(' ', input.KnownBugs.Select(b => b.Id)) : null;
        await UpsertKnownBugsAsync(input.KnownBugs, ct);
        await db.SaveChangesAsync(ct);

        await RecountAsync(runId, ct);
        await BroadcastScenarioAsync(resultId, ct);
    }

    public async Task EndAsync(int runId, IngestEndInput input, CancellationToken ct)
    {
        var status = input.Status?.ToLowerInvariant() switch
        {
            "interrupted" or "stopped" => RunStatus.Stopped,
            "error" or "timedout" => RunStatus.Error,
            _ => RunStatus.Completed,
        };
        await FinalizeAsync(runId, status, input.Reason, ct);
    }

    // ------------------------------------------------------------------ finalisation
    /// <summary>
    /// Closes a run. Idempotent: the runner's "end", the process-exit watcher and a user's Stop may all
    /// arrive — the first one wins. Unfinished scenarios become Interrupted (stopped) or Skipped.
    /// </summary>
    public async Task FinalizeAsync(int runId, RunStatus status, string? reason, CancellationToken ct)
    {
        var run = await db.Runs.FirstOrDefaultAsync(r => r.Id == runId, ct) ?? throw new NotFoundException($"Run {runId} not found");
        if (run.FinishedAt is not null) return;

        var now = DateTimeOffset.UtcNow;
        var open = await db.ScenarioResults.Where(r => r.RunId == runId && r.FinishedAt == null).ToListAsync(ct);
        var openStatus = status == RunStatus.Completed ? ScenarioStatus.Skipped : ScenarioStatus.Interrupted;
        foreach (var result in open)
        {
            result.Status = openStatus;
            result.FinishedAt = now;
            result.ErrorMessage ??= status == RunStatus.Stopped ? "Run was stopped before this scenario finished." : reason;
        }
        await db.SaveChangesAsync(ct);

        await ApplyCountsAsync(run, ct);
        if (status == RunStatus.Completed && run.Total == 0)
        {
            // The runner exited without announcing a single scenario (e.g. a filter matched nothing).
            status = RunStatus.Error;
            reason ??= "No scenarios were reported — see the runner output.";
        }
        run.Status = status;
        run.StatusReason = reason;
        run.StartedAt ??= run.CreatedAt;
        run.FinishedAt = now;
        run.DurationMs = (long)(now - run.StartedAt.Value).TotalMilliseconds;
        await db.SaveChangesAsync(ct);

        foreach (var result in open) await BroadcastScenarioAsync(result.Id, ct);
        await events.RunUpdated(run.ToSummary());
        logger.LogInformation("Run {RunId} finalised as {Status}", runId, status);
    }

    // ------------------------------------------------------------------ queries used by controllers
    public async Task<RunDetailDto> GetDetailAsync(int runId, CancellationToken ct)
    {
        var run = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct) ?? throw new NotFoundException($"Run {runId} not found");
        var bugs = await KnownBugMapAsync(ct);
        var scenarios = await db.ScenarioResults.AsNoTracking()
            .Where(r => r.RunId == runId)
            .Include(r => r.TestCase)
            .Include(r => r.Evidence)
            .OrderBy(r => r.Ordinal)
            .ToListAsync(ct);
        return new RunDetailDto(run.ToSummary(), scenarios.Select(s => s.ToDto(bugs)).ToList());
    }

    /// <summary>One scenario result with its evidence — used to open evidence from the history view (TM-08).</summary>
    public async Task<ScenarioDto> GetResultAsync(int resultId, CancellationToken ct)
    {
        var result = await db.ScenarioResults.AsNoTracking()
                         .Include(r => r.TestCase).Include(r => r.Evidence)
                         .FirstOrDefaultAsync(r => r.Id == resultId, ct)
                     ?? throw new NotFoundException($"Result {resultId} not found");
        return result.ToDto(await KnownBugMapAsync(ct));
    }

    public async Task<IReadOnlyDictionary<string, KnownBug>> KnownBugMapAsync(CancellationToken ct) =>
        await db.KnownBugs.AsNoTracking().ToDictionaryAsync(b => b.Id, ct);

    // ------------------------------------------------------------------ helpers
    private async Task<Run> GetOpenRunAsync(int runId, CancellationToken ct)
    {
        var run = await db.Runs.FirstOrDefaultAsync(r => r.Id == runId, ct) ?? throw new NotFoundException($"Run {runId} not found");
        if (run.FinishedAt is not null) throw new ConflictException($"Run {runId} is already finished ({run.Status}) and can no longer change");
        return run;
    }

    private async Task<ScenarioResult> GetOpenResultAsync(int runId, int resultId, CancellationToken ct)
    {
        await GetOpenRunAsync(runId, ct);
        var result = await db.ScenarioResults.FirstOrDefaultAsync(r => r.Id == resultId && r.RunId == runId, ct)
                     ?? throw new NotFoundException($"Result {resultId} not found in run {runId}");
        if (result.FinishedAt is not null) throw new ConflictException($"Result {resultId} is final and cannot change");
        return result;
    }

    /// <summary>Totals are always recomputed from the results (the source of truth), never incremented.</summary>
    private async Task RecountAsync(int runId, CancellationToken ct)
    {
        var run = await db.Runs.FirstAsync(r => r.Id == runId, ct);
        await ApplyCountsAsync(run, ct);
        await db.SaveChangesAsync(ct);
        await events.RunUpdated(run.ToSummary());
    }

    private async Task ApplyCountsAsync(Run run, CancellationToken ct)
    {
        var counts = await db.ScenarioResults.Where(r => r.RunId == run.Id)
            .GroupBy(r => r.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int Count(params ScenarioStatus[] s) => counts.Where(c => s.Contains(c.Key)).Sum(c => c.Count);
        run.Passed = Count(ScenarioStatus.Passed);
        run.Failed = Count(ScenarioStatus.Failed);
        run.ExpectedFailures = Count(ScenarioStatus.ExpectedFailure);
        run.UnexpectedPasses = Count(ScenarioStatus.UnexpectedPass);
        run.Skipped = Count(ScenarioStatus.Skipped, ScenarioStatus.Interrupted);
    }

    private async Task UpsertKnownBugsAsync(IReadOnlyList<KnownBugDto>? bugs, CancellationToken ct)
    {
        if (bugs is null) return;
        foreach (var bug in bugs.Where(b => !string.IsNullOrWhiteSpace(b.Id)))
        {
            var entity = await db.KnownBugs.FindAsync([bug.Id], ct);
            if (entity is null) db.KnownBugs.Add(entity = new KnownBug { Id = bug.Id });
            if (!string.IsNullOrWhiteSpace(bug.Title)) entity.Title = bug.Title;
            if (!string.IsNullOrWhiteSpace(bug.Severity)) entity.Severity = bug.Severity;
            entity.LastSeenAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task BroadcastScenarioAsync(int resultId, CancellationToken ct)
    {
        var result = await db.ScenarioResults.AsNoTracking()
            .Include(r => r.TestCase).Include(r => r.Evidence)
            .FirstAsync(r => r.Id == resultId, ct);
        await events.ScenarioUpdated(result.ToDto(await KnownBugMapAsync(ct)));
    }

    public static string NormaliseUrl(string url) => url.Trim().TrimEnd('/').ToLowerInvariant();

    private static string? Truncate(string? value, int max) => value is null || value.Length <= max ? value : value[..max] + "…";
}
