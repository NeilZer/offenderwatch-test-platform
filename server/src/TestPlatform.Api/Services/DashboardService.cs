using Microsoft.EntityFrameworkCore;
using TestPlatform.Api.Contracts;
using TestPlatform.Api.Data;
using TestPlatform.Api.Domain;
using TestPlatform.Api.Infrastructure;

namespace TestPlatform.Api.Services;

/// <summary>TM-07 — the landing page. Designed to replace the static Part 4 dashboard as the go/no-go source.</summary>
public class DashboardService(PlatformDbContext db, HistoryService history, RunService runs)
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var environments = await db.Environments.AsNoTracking().OrderByDescending(e => e.IsDefault).ThenBy(e => e.Name).ToListAsync(ct);
        var allRuns = await db.Runs.AsNoTracking().OrderByDescending(r => r.Id).Take(500).ToListAsync(ct);

        // The headline per environment is its latest COMPLETE run (a 1-test run must not show "100 %");
        // newer partial runs are reported next to it. Before any complete run exists: the latest run.
        var latestPerEnv = environments
            .Select(e => LatestPerEnvironment(e, allRuns))
            .Where(x => x is not null).Select(x => x!).ToList();

        // Every completed run, oldest → newest. Partial runs are flagged: the client lets the reader
        // include or exclude them (their pass rate is not comparable with a complete run's).
        var trend = allRuns.Where(r => r.Status == RunStatus.Completed && r.Total > 0).Take(40).Reverse()
            .Select(ToTrendPoint).ToList();

        var bugs = await runs.KnownBugMapAsync(ct);
        var failing = await CurrentlyFailingAsync(bugs, ct);
        var verdict = await VerdictAsync(environments.FirstOrDefault(e => e.IsDefault) ?? environments.FirstOrDefault(), allRuns, bugs, ct);
        var active = allRuns.Where(r => r.FinishedAt is null).Select(r => r.ToSummary()).ToList();
        var notifications = await db.Notifications.AsNoTracking().OrderByDescending(n => n.Id).Take(5).ToListAsync(ct);

        return new DashboardDto(verdict, latestPerEnv, trend, failing, active, notifications.Select(n => n.ToDto()).ToList());
    }

    private async Task<List<FailingTestDto>> CurrentlyFailingAsync(IReadOnlyDictionary<string, KnownBug> bugs, CancellationToken ct)
    {
        var points = await history.LoadPointsAsync(null, ct);
        var tests = await db.TestCases.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);
        var latestBugs = await db.ScenarioResults.AsNoTracking()
            .Where(r => r.FinishedAt != null && r.KnownBugs != null)
            .Select(r => new { r.Id, r.KnownBugs }).ToDictionaryAsync(r => r.Id, r => r.KnownBugs, ct);

        var rows = new List<FailingTestDto>();
        foreach (var group in points.GroupBy(p => p.TestCaseId))
        {
            var pointList = group.Select(g => g.Point).ToList();
            foreach (var summary in HistoryAnalyzer.Summarize(pointList).Where(s => s.Current == Verdict.Fail))
            {
                var lastResult = pointList.Where(p => p.EnvironmentId == summary.EnvironmentId && HistoryAnalyzer.ToVerdict(p.Status) != Verdict.NotRun)
                    .OrderBy(p => p.At).Last();
                var t = tests[group.Key];
                rows.Add(new FailingTestDto(t.Id, t.Title, t.Tags, summary.EnvironmentName, summary.CurrentStatus?.ToString() ?? "",
                    summary.CurrentStatus == ScenarioStatus.ExpectedFailure, summary.FailingSince, summary.FailingSinceRunId, summary.LastPassedAt,
                    Mapper.ResolveBugs(latestBugs.GetValueOrDefault(lastResult.ResultId), bugs), summary.IsFlaky));
            }
        }
        // Unexpected failures first (need attention), then the longest-failing.
        return rows.OrderBy(r => r.Expected).ThenBy(r => r.FailingSince).ToList();
    }

    private static LatestRunDto? LatestPerEnvironment(TargetEnvironment env, List<Run> allRuns)
    {
        var runs = allRuns.Where(r => r.EnvironmentId == env.Id).ToList(); // newest first
        if (runs.Count == 0) return null;
        var complete = runs.FirstOrDefault(r => r.Status == RunStatus.Completed && r.IsFullSuite && r.Skipped == 0);
        var headline = complete ?? runs[0];
        var newerPartial = runs.TakeWhile(r => r.Id != headline.Id).Select(r => r.Id).ToList();
        return new LatestRunDto(headline.ToSummary(), complete is not null, newerPartial);
    }

    private static TrendPointDto ToTrendPoint(Run r)
    {
        var pass = r.Passed + r.UnexpectedPasses;
        var executed = pass + r.Failed + r.ExpectedFailures;
        return new TrendPointDto(r.Id, r.EnvironmentId, r.EnvironmentName, r.FinishedAt ?? r.CreatedAt,
            executed == 0 ? 0 : Math.Round(100.0 * pass / executed, 1), pass, executed,
            r.IsFullSuite && r.Skipped == 0, r.Scope, r.Filter, r.Skipped);
    }

    /// <summary>
    /// Go / no-go on the default environment from the LATEST RESULT OF EVERY TEST (any run, partial or full) —
    /// see <see cref="ReleaseVerdict"/>. The basis is returned too, including how the verdict compares with the
    /// last complete run, so the reader sees which runs it was built from.
    /// </summary>
    private async Task<VerdictDto> VerdictAsync(TargetEnvironment? env, List<Run> allRuns, IReadOnlyDictionary<string, KnownBug> bugs, CancellationToken ct)
    {
        if (env is null) return new VerdictDto(false, "No environment configured yet.", null, null);

        var baseline = allRuns.FirstOrDefault(r => r.EnvironmentId == env.Id && r.Status == RunStatus.Completed && r.IsFullSuite && r.Skipped == 0);
        var latest = await LatestResultPerTestAsync(env.Id, ct);
        if (latest.Count == 0) return new VerdictDto(false, $"No test has run on {env.Name} yet — no verdict.", env.Name, null);

        // The current suite = the tests of the last complete run (tests removed from the code drop out);
        // before any complete run exists: every test ever run here.
        var suite = baseline is null
            ? latest.Keys.ToList()
            : await db.ScenarioResults.AsNoTracking().Where(r => r.RunId == baseline.Id).Select(r => r.TestCaseId).ToListAsync(ct);

        var decision = ReleaseVerdict.Decide(suite, latest.Values, id => bugs.TryGetValue(id, out var b) ? b.Severity : null);
        var (improved, worsened) = baseline is null ? (0, 0) : await CompareWithBaselineAsync(baseline.Id, latest, ct);

        var line = decision.Go
            ? $"GO — {env.Name}: all {decision.TestsConsidered} tests have a result, no unexpected failure and no Critical/High bug open; {decision.PassRate}% pass."
            : $"NO-GO — {env.Name}: " + string.Join(", ", ReasonParts(decision)) + $"; {decision.PassRate}% of tests pass.";
        var basis = new VerdictBasisDto(decision.TestsConsidered, decision.TestsWithResult, decision.TestsNeverRun, decision.Passing,
            decision.UnexpectedFailures, decision.BlockingBugIds, decision.SourceRunIds, baseline?.Id, improved, worsened);
        return new VerdictDto(decision.Go, line, env.Name, decision.NewestRunId, basis);
    }

    private static IEnumerable<string> ReasonParts(ReleaseDecision d)
    {
        if (d.CriticalBugs + d.HighBugs > 0) yield return $"{d.CriticalBugs} Critical and {d.HighBugs} High documented bugs still fail";
        if (d.UnexpectedFailures > 0) yield return $"{d.UnexpectedFailures} unexpected failure(s)";
        if (d.TestsNeverRun > 0) yield return $"{d.TestsNeverRun} test(s) have never run here";
    }

    /// <summary>Newest EXECUTED result of each test on one environment (skipped / stopped results don't count).</summary>
    private async Task<Dictionary<int, LatestTestResult>> LatestResultPerTestAsync(int environmentId, CancellationToken ct)
    {
        var rows = await db.ScenarioResults.AsNoTracking()
            .Where(r => r.Run!.EnvironmentId == environmentId && r.FinishedAt != null
                        && r.Status != ScenarioStatus.Skipped && r.Status != ScenarioStatus.Interrupted)
            .Select(r => new { r.TestCaseId, r.RunId, r.FinishedAt, r.Status, r.KnownBugs })
            .ToListAsync(ct);
        return rows
            .GroupBy(r => r.TestCaseId)
            .Select(g => g.OrderByDescending(r => r.FinishedAt).ThenByDescending(r => r.RunId).First())
            .ToDictionary(r => r.TestCaseId, r => new LatestTestResult(r.TestCaseId, r.RunId, r.FinishedAt!.Value, r.Status,
                (r.KnownBugs ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    /// <summary>How many tests pass now but failed in the last complete run, and the other way round.</summary>
    private async Task<(int Improved, int Worsened)> CompareWithBaselineAsync(int baselineRunId, Dictionary<int, LatestTestResult> latest, CancellationToken ct)
    {
        var baseline = await db.ScenarioResults.AsNoTracking().Where(r => r.RunId == baselineRunId)
            .Select(r => new { r.TestCaseId, r.Status }).ToListAsync(ct);
        int improved = 0, worsened = 0;
        foreach (var b in baseline)
        {
            if (!latest.TryGetValue(b.TestCaseId, out var now)) continue;
            var before = HistoryAnalyzer.ToVerdict(b.Status);
            var after = HistoryAnalyzer.ToVerdict(now.Status);
            if (before == Verdict.Fail && after == Verdict.Pass) improved++;
            else if (before == Verdict.Pass && after == Verdict.Fail) worsened++;
        }
        return (improved, worsened);
    }
}
