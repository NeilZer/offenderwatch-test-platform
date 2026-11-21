using Microsoft.EntityFrameworkCore;
using TestPlatform.Api.Contracts;
using TestPlatform.Api.Data;
using TestPlatform.Api.Domain;
using TestPlatform.Api.Infrastructure;

namespace TestPlatform.Api.Services;

/// <summary>TM-04 / B-01 — per-test history across runs, transitions and flakiness.</summary>
public class HistoryService(PlatformDbContext db, RunService runs)
{
    public static readonly FlakinessRuleDto Rule = new(HistoryAnalyzer.FlakinessWindow, HistoryAnalyzer.FlakinessMinFlips,
        $"Flaky = the pass/fail verdict changed {HistoryAnalyzer.FlakinessMinFlips}+ times within the last {HistoryAnalyzer.FlakinessWindow} " +
        "runs on the same environment, counting only consecutive runs with the same suite version (no code change).");

    public async Task<IReadOnlyList<TestCaseListItemDto>> ListAsync(CancellationToken ct)
    {
        var testCases = await db.TestCases.AsNoTracking().OrderBy(t => t.Project).ThenBy(t => t.File).ThenBy(t => t.Id).ToListAsync(ct);
        var points = await LoadPointsAsync(null, ct);
        var byTest = points.ToLookup(p => p.TestCaseId);
        return testCases.Select(t => ToListItem(t, byTest[t.Id].Select(p => p.Point).ToList())).ToList();
    }

    public async Task<TestHistoryDto> GetAsync(int testCaseId, CancellationToken ct)
    {
        var testCase = await db.TestCases.AsNoTracking().FirstOrDefaultAsync(t => t.Id == testCaseId, ct)
                       ?? throw new NotFoundException($"Test case {testCaseId} not found");
        var rows = await db.ScenarioResults.AsNoTracking()
            .Where(r => r.TestCaseId == testCaseId && r.FinishedAt != null)
            .Select(r => new
            {
                r.Id, r.RunId, r.Run!.EnvironmentId, r.Run.EnvironmentName, r.FinishedAt, r.Status, r.Run.SuiteVersion,
                r.DurationMs, r.ErrorMessage, r.KnownBugs, EvidenceCount = r.Evidence.Count,
            })
            .ToListAsync(ct);
        var points = rows.Select(r => new HistoryPoint(r.Id, r.RunId, r.EnvironmentId, r.EnvironmentName, r.FinishedAt!.Value, r.Status, r.SuiteVersion)).ToList();
        var analyzed = HistoryAnalyzer.Analyze(points);
        var byId = rows.ToDictionary(r => r.Id);
        var bugs = await runs.KnownBugMapAsync(ct);

        var entries = analyzed.Select(a =>
        {
            var row = byId[a.Point.ResultId];
            return new HistoryEntryDto(a.Point.ResultId, a.Point.RunId, a.Point.EnvironmentId, a.Point.EnvironmentName, a.Point.At,
                a.Point.Status.ToString(), a.Verdict.ToString(), a.Transition.ToString(), row.DurationMs, row.ErrorMessage,
                Mapper.ResolveBugs(row.KnownBugs, bugs), a.Point.SuiteVersion, row.EvidenceCount);
        }).ToList();
        return new TestHistoryDto(ToListItem(testCase, points), entries, Rule);
    }

    internal async Task<List<(int TestCaseId, HistoryPoint Point)>> LoadPointsAsync(int? environmentId, CancellationToken ct)
    {
        var query = db.ScenarioResults.AsNoTracking().Where(r => r.FinishedAt != null);
        if (environmentId is not null) query = query.Where(r => r.Run!.EnvironmentId == environmentId);
        var rows = await query
            .Select(r => new { r.TestCaseId, r.Id, r.RunId, r.Run!.EnvironmentId, r.Run.EnvironmentName, r.FinishedAt, r.Status, r.Run.SuiteVersion })
            .ToListAsync(ct);
        return rows.Select(r => (r.TestCaseId, new HistoryPoint(r.Id, r.RunId, r.EnvironmentId, r.EnvironmentName, r.FinishedAt!.Value, r.Status, r.SuiteVersion))).ToList();
    }

    private static TestCaseListItemDto ToListItem(TestCase t, IReadOnlyList<HistoryPoint> points) =>
        new(t.Id, t.Title, t.File, t.Project, t.Tags, points.Count,
            points.Count == 0 ? Array.Empty<EnvironmentSummaryDto>() : HistoryAnalyzer.Summarize(points).Select(s => s.ToDto()).ToList());
}
