namespace TestPlatform.Api.Domain;

/// <summary>The newest EXECUTED result of one test on one environment (from whichever run produced it).</summary>
public record LatestTestResult(int TestCaseId, int RunId, DateTimeOffset At, ScenarioStatus Status, IReadOnlyList<string> KnownBugIds);

public record ReleaseDecision(
    bool Go,
    int TestsConsidered,
    int TestsWithResult,
    int TestsNeverRun,
    int Passing,
    int UnexpectedFailures,
    IReadOnlyList<string> BlockingBugIds,
    int CriticalBugs,
    int HighBugs,
    int PassRate,
    IReadOnlyList<int> SourceRunIds,
    int? NewestRunId);

/// <summary>
/// TM-07 go / no-go, decided from the LATEST RESULT OF EVERY TEST on the environment — not from one run.
///
/// Why: a newer run (even a partial one: API only, one filtered test) updates the tests it executed, so the
/// verdict always reflects the newest evidence. Tests a partial run did not execute keep their last known
/// result, so "1 of 1 passed" can never make a release GO on its own.
///
/// GO only when EVERY test of the current suite has a result, none failed unexpectedly, and no test fails
/// on a documented bug of a blocking severity (Critical / High).
/// </summary>
public static class ReleaseVerdict
{
    public static readonly IReadOnlySet<string> BlockingSeverities = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Critical", "High" };

    /// <param name="suite">Test cases that make up the current suite (what must be covered).</param>
    /// <param name="latest">Newest executed result per test on the environment (any run).</param>
    /// <param name="severityOf">Bug id → severity (null when unknown).</param>
    public static ReleaseDecision Decide(IReadOnlyCollection<int> suite, IEnumerable<LatestTestResult> latest, Func<string, string?> severityOf)
    {
        var bySuite = latest.Where(l => suite.Contains(l.TestCaseId)).ToDictionary(l => l.TestCaseId);
        var passing = bySuite.Values.Count(l => HistoryAnalyzer.ToVerdict(l.Status) == Verdict.Pass);
        var unexpected = bySuite.Values.Count(l => l.Status == ScenarioStatus.Failed);

        var failingBugIds = bySuite.Values
            .Where(l => l.Status == ScenarioStatus.ExpectedFailure)
            .SelectMany(l => l.KnownBugIds)
            .Distinct()
            .ToList();
        var blocking = failingBugIds.Where(id => BlockingSeverities.Contains(severityOf(id) ?? "")).OrderBy(id => id).ToList();
        var critical = failingBugIds.Count(id => string.Equals(severityOf(id), "Critical", StringComparison.OrdinalIgnoreCase));
        var high = failingBugIds.Count(id => string.Equals(severityOf(id), "High", StringComparison.OrdinalIgnoreCase));

        var neverRun = suite.Count - bySuite.Count;
        var executed = bySuite.Count;
        var go = suite.Count > 0 && neverRun == 0 && unexpected == 0 && blocking.Count == 0;

        return new ReleaseDecision(
            go, suite.Count, executed, neverRun, passing, unexpected, blocking, critical, high,
            executed == 0 ? 0 : (int)Math.Round(100.0 * passing / executed),
            bySuite.Values.Select(l => l.RunId).Distinct().OrderBy(id => id).ToList(),
            bySuite.Count == 0 ? null : bySuite.Values.Max(l => l.RunId));
    }
}
