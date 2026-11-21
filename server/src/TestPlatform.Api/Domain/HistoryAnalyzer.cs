namespace TestPlatform.Api.Domain;

/// <summary>
/// Product-level verdict of one result: does the REQUIREMENT work?
///   Pass   = Passed, or UnexpectedPass (an expected-to-fail check now passes → the bug looks fixed)
///   Fail   = Failed (unexpected) or ExpectedFailure (fails on a documented bug)
///   NotRun = Skipped / Interrupted / still queued — ignored when computing transitions
/// This matches the Part 4 dashboard: a test case failing on a known bug is a failed test case.
/// The expected/unexpected distinction is kept separately (Status) and is always shown.
/// </summary>
public enum Verdict { Pass, Fail, NotRun }

public enum Transition { None, FirstResult, Regression, Recovery }

public record HistoryPoint(
    int ResultId,
    int RunId,
    int? EnvironmentId,
    string EnvironmentName,
    DateTimeOffset At,
    ScenarioStatus Status,
    string? SuiteVersion);

public record AnalyzedPoint(HistoryPoint Point, Verdict Verdict, Transition Transition);

public record EnvironmentSummary(
    int? EnvironmentId,
    string EnvironmentName,
    Verdict Current,
    ScenarioStatus? CurrentStatus,
    DateTimeOffset? LastPassedAt,
    DateTimeOffset? FailingSince,
    int? FailingSinceRunId,
    bool IsFlaky,
    int Flips,
    int Executions);

public static class HistoryAnalyzer
{
    /// <summary>B-01: how many recent executions per environment are inspected.</summary>
    public const int FlakinessWindow = 10;

    /// <summary>B-01: verdict changes needed inside the window (pass→fail→pass = 2).</summary>
    public const int FlakinessMinFlips = 2;

    public static Verdict ToVerdict(ScenarioStatus status) => status switch
    {
        ScenarioStatus.Passed or ScenarioStatus.UnexpectedPass => Verdict.Pass,
        ScenarioStatus.Failed or ScenarioStatus.ExpectedFailure => Verdict.Fail,
        _ => Verdict.NotRun,
    };

    public static bool IsFinal(ScenarioStatus status) =>
        status is not (ScenarioStatus.Queued or ScenarioStatus.Running);

    /// <summary>
    /// Marks every point with its transition, comparing each executed result with the previous
    /// EXECUTED result of the same test on the SAME environment (results on another environment
    /// are a different context, not a regression). Input order does not matter; output is chronological.
    /// </summary>
    public static IReadOnlyList<AnalyzedPoint> Analyze(IEnumerable<HistoryPoint> points)
    {
        var ordered = points.OrderBy(p => p.At).ThenBy(p => p.ResultId).ToList();
        var lastVerdictByEnv = new Dictionary<string, Verdict>();
        var result = new List<AnalyzedPoint>(ordered.Count);

        foreach (var point in ordered)
        {
            var verdict = ToVerdict(point.Status);
            var envKey = EnvKey(point);
            var transition = Transition.None;
            if (verdict != Verdict.NotRun)
            {
                if (!lastVerdictByEnv.TryGetValue(envKey, out var previous)) transition = Transition.FirstResult;
                else if (previous == Verdict.Pass && verdict == Verdict.Fail) transition = Transition.Regression;
                else if (previous == Verdict.Fail && verdict == Verdict.Pass) transition = Transition.Recovery;
                lastVerdictByEnv[envKey] = verdict;
            }
            result.Add(new AnalyzedPoint(point, verdict, transition));
        }
        return result;
    }

    /// <summary>Per-environment answers to: is it failing now, since when, when did it last pass, is it flaky?</summary>
    public static IReadOnlyList<EnvironmentSummary> Summarize(IEnumerable<HistoryPoint> points)
    {
        var analyzed = Analyze(points);
        return analyzed
            .GroupBy(a => EnvKey(a.Point))
            .Select(group => SummarizeEnvironment(group.ToList()))
            .OrderBy(s => s.EnvironmentName)
            .ToList();
    }

    private static EnvironmentSummary SummarizeEnvironment(List<AnalyzedPoint> chronological)
    {
        var executed = chronological.Where(a => a.Verdict != Verdict.NotRun).ToList();
        var first = chronological[0].Point;
        if (executed.Count == 0)
        {
            return new EnvironmentSummary(first.EnvironmentId, first.EnvironmentName, Verdict.NotRun, chronological[^1].Point.Status,
                null, null, null, false, 0, 0);
        }

        var current = executed[^1];
        var lastPass = executed.LastOrDefault(a => a.Verdict == Verdict.Pass);

        // Still-failing-since = the first failure of the CURRENT unbroken failing streak.
        AnalyzedPoint? streakStart = null;
        if (current.Verdict == Verdict.Fail)
        {
            for (var i = executed.Count - 1; i >= 0 && executed[i].Verdict == Verdict.Fail; i--) streakStart = executed[i];
        }

        var (isFlaky, flips) = DetectFlakiness(executed.Select(a => a.Point).ToList());
        return new EnvironmentSummary(first.EnvironmentId, first.EnvironmentName, current.Verdict, current.Point.Status,
            lastPass?.Point.At, streakStart?.Point.At, streakStart?.Point.RunId, isFlaky, flips, executed.Count);
    }

    /// <summary>
    /// B-01 rule: within the last <see cref="FlakinessWindow"/> executions on one environment, count
    /// verdict changes between consecutive runs that used the SAME suite version (= no code change).
    /// Two or more changes (e.g. pass → fail → pass) ⇒ flaky. A single change is a real transition
    /// (regression or recovery), not flakiness; a change across suite versions is explained by the code.
    /// </summary>
    public static (bool IsFlaky, int Flips) DetectFlakiness(IReadOnlyList<HistoryPoint> executedChronological)
    {
        var window = executedChronological.TakeLast(FlakinessWindow).ToList();
        var flips = 0;
        for (var i = 1; i < window.Count; i++)
        {
            var previous = window[i - 1];
            var current = window[i];
            var sameCode = previous.SuiteVersion is not null && previous.SuiteVersion == current.SuiteVersion;
            if (sameCode && ToVerdict(previous.Status) != ToVerdict(current.Status)) flips++;
        }
        return (flips >= FlakinessMinFlips, flips);
    }

    private static string EnvKey(HistoryPoint p) => p.EnvironmentId?.ToString() ?? $"name:{p.EnvironmentName}";
}
