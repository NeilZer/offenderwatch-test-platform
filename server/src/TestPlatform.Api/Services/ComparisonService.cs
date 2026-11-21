using Microsoft.EntityFrameworkCore;
using TestPlatform.Api.Contracts;
using TestPlatform.Api.Data;
using TestPlatform.Api.Domain;
using TestPlatform.Api.Infrastructure;

namespace TestPlatform.Api.Services;

/// <summary>B-02 — what changed between two runs.</summary>
public class ComparisonService(PlatformDbContext db)
{
    public async Task<RunComparisonDto> CompareAsync(int baseRunId, int targetRunId, CancellationToken ct)
    {
        var baseRun = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == baseRunId, ct) ?? throw new NotFoundException($"Run {baseRunId} not found");
        var targetRun = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == targetRunId, ct) ?? throw new NotFoundException($"Run {targetRunId} not found");
        var baseResults = await LoadAsync(baseRunId, ct);
        var targetResults = await LoadAsync(targetRunId, ct);

        var regressed = new List<ComparisonRowDto>();
        var recovered = new List<ComparisonRowDto>();
        var changed = new List<ComparisonRowDto>();
        var added = new List<ComparisonRowDto>();
        var unchanged = 0;

        foreach (var (id, target) in targetResults)
        {
            if (!baseResults.TryGetValue(id, out var before))
            {
                added.Add(Row(id, target, null, target.Status));
                continue;
            }
            var vb = HistoryAnalyzer.ToVerdict(before.Status);
            var vt = HistoryAnalyzer.ToVerdict(target.Status);
            if (vb == Verdict.Pass && vt == Verdict.Fail) regressed.Add(Row(id, target, before.Status, target.Status));
            else if (vb == Verdict.Fail && vt == Verdict.Pass) recovered.Add(Row(id, target, before.Status, target.Status));
            else if (before.Status != target.Status) changed.Add(Row(id, target, before.Status, target.Status));
            else unchanged++;
        }
        var missing = baseResults.Where(kv => !targetResults.ContainsKey(kv.Key)).Select(kv => Row(kv.Key, kv.Value, kv.Value.Status, null)).ToList();

        var delta = new RunTotals(targetRun.Total - baseRun.Total, targetRun.Passed - baseRun.Passed, targetRun.Failed - baseRun.Failed,
            targetRun.ExpectedFailures - baseRun.ExpectedFailures, targetRun.UnexpectedPasses - baseRun.UnexpectedPasses, targetRun.Skipped - baseRun.Skipped);
        return new RunComparisonDto(baseRun.ToSummary(), targetRun.ToSummary(), regressed, recovered, changed, added, missing, unchanged, delta);
    }

    private sealed record Item(string Title, string Project, ScenarioStatus Status);

    private async Task<Dictionary<int, Item>> LoadAsync(int runId, CancellationToken ct) =>
        await db.ScenarioResults.AsNoTracking().Where(r => r.RunId == runId)
            .Select(r => new { r.TestCaseId, r.TestCase!.Title, r.TestCase.Project, r.Status })
            .ToDictionaryAsync(r => r.TestCaseId, r => new Item(r.Title, r.Project, r.Status), ct);

    private static ComparisonRowDto Row(int id, Item item, ScenarioStatus? before, ScenarioStatus? after) =>
        new(id, item.Title, item.Project, before?.ToString(), after?.ToString());
}
