using TestPlatform.Api.Domain;
using static TestPlatform.Api.Domain.ScenarioStatus;

namespace TestPlatform.Tests;

/// <summary>B-06 — TM-04 transition logic and B-01 flakiness rule.</summary>
public class HistoryAnalyzerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a chronological history on one environment: each status is one run, one hour apart.</summary>
    private static List<HistoryPoint> History(params ScenarioStatus[] statuses) =>
        statuses.Select((s, i) => Point(i, s)).ToList();

    private static HistoryPoint Point(int i, ScenarioStatus status, int env = 1, string version = "v1") =>
        new(ResultId: 100 + i, RunId: i + 1, EnvironmentId: env, EnvironmentName: $"env{env}", At: T0.AddHours(i), Status: status, SuiteVersion: version);

    [Fact]
    public void First_executed_result_is_marked_FirstResult()
    {
        var analyzed = HistoryAnalyzer.Analyze(History(Passed));
        Assert.Equal(Transition.FirstResult, analyzed[0].Transition);
    }

    [Fact]
    public void Pass_then_fail_is_a_regression()
    {
        var analyzed = HistoryAnalyzer.Analyze(History(Passed, Failed));
        Assert.Equal(Transition.Regression, analyzed[1].Transition);
    }

    [Fact]
    public void Fail_then_pass_is_a_recovery()
    {
        var analyzed = HistoryAnalyzer.Analyze(History(Failed, Passed));
        Assert.Equal(Transition.Recovery, analyzed[1].Transition);
    }

    [Fact]
    public void Known_bug_counts_as_failing_and_bug_fixed_counts_as_passing()
    {
        // ExpectedFailure = the requirement still fails; UnexpectedPass = it works now (bug fixed).
        var analyzed = HistoryAnalyzer.Analyze(History(ExpectedFailure, UnexpectedPass, ExpectedFailure));
        Assert.Equal(new[] { Transition.FirstResult, Transition.Recovery, Transition.Regression }, analyzed.Select(a => a.Transition));
    }

    [Fact]
    public void Skipped_and_interrupted_results_are_ignored_for_transitions()
    {
        var analyzed = HistoryAnalyzer.Analyze(History(Passed, Interrupted, Skipped, Passed));
        Assert.Equal(new[] { Transition.FirstResult, Transition.None, Transition.None, Transition.None }, analyzed.Select(a => a.Transition));
    }

    [Fact]
    public void Transitions_are_computed_per_environment()
    {
        // Passing on env 1 and failing on env 2 is a different context, not a regression.
        var points = new[] { Point(0, Passed, env: 1), Point(1, Failed, env: 2), Point(2, Passed, env: 1), Point(3, Failed, env: 2) };
        var analyzed = HistoryAnalyzer.Analyze(points);
        Assert.DoesNotContain(analyzed, a => a.Transition is Transition.Regression or Transition.Recovery);
    }

    [Fact]
    public void Input_order_does_not_matter()
    {
        var shuffled = History(Passed, Failed, Passed).AsEnumerable().Reverse();
        var analyzed = HistoryAnalyzer.Analyze(shuffled);
        Assert.Equal(new[] { 100, 101, 102 }, analyzed.Select(a => a.Point.ResultId));
        Assert.Equal(Transition.Regression, analyzed[1].Transition);
    }

    [Fact]
    public void Failing_since_is_the_first_failure_of_the_current_streak()
    {
        var summary = HistoryAnalyzer.Summarize(History(Failed, Passed, Failed, ExpectedFailure, Failed)).Single();
        Assert.Equal(Verdict.Fail, summary.Current);
        Assert.Equal(T0.AddHours(2), summary.FailingSince);   // the failure right after the last pass
        Assert.Equal(3, summary.FailingSinceRunId);
        Assert.Equal(T0.AddHours(1), summary.LastPassedAt);
    }

    [Fact]
    public void A_currently_passing_test_has_no_failing_since()
    {
        var summary = HistoryAnalyzer.Summarize(History(Failed, Passed)).Single();
        Assert.Equal(Verdict.Pass, summary.Current);
        Assert.Null(summary.FailingSince);
        Assert.Equal(T0.AddHours(1), summary.LastPassedAt);
    }

    [Fact]
    public void Never_passed_test_reports_no_last_pass()
    {
        var summary = HistoryAnalyzer.Summarize(History(ExpectedFailure, ExpectedFailure)).Single();
        Assert.Null(summary.LastPassedAt);
        Assert.Equal(T0, summary.FailingSince);
    }

    [Fact]
    public void Pass_fail_pass_with_the_same_code_is_flaky()
    {
        var (isFlaky, flips) = HistoryAnalyzer.DetectFlakiness(History(Passed, Failed, Passed));
        Assert.True(isFlaky);
        Assert.Equal(2, flips);
    }

    [Fact]
    public void A_single_change_is_a_transition_not_flakiness()
    {
        var (isFlaky, flips) = HistoryAnalyzer.DetectFlakiness(History(Passed, Passed, Failed, Failed));
        Assert.False(isFlaky);
        Assert.Equal(1, flips);
    }

    [Fact]
    public void Changes_explained_by_a_code_change_are_not_flakiness()
    {
        var points = new[] { Point(0, Passed, version: "v1"), Point(1, Failed, version: "v2"), Point(2, Passed, version: "v3") };
        Assert.False(HistoryAnalyzer.DetectFlakiness(points).IsFlaky);
    }

    [Fact]
    public void Only_the_recent_window_is_considered()
    {
        // Old alternation, then 10 stable passes: no longer flaky.
        var statuses = new[] { Passed, Failed, Passed, Failed }.Concat(Enumerable.Repeat(Passed, HistoryAnalyzer.FlakinessWindow)).ToArray();
        Assert.False(HistoryAnalyzer.DetectFlakiness(History(statuses)).IsFlaky);
    }
}
