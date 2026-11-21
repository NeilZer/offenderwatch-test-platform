using TestPlatform.Api.Domain;
using static TestPlatform.Api.Domain.ScenarioStatus;

namespace TestPlatform.Tests;

/// <summary>TM-07 — go / no-go from the latest result of every test.</summary>
public class ReleaseVerdictTests
{
    private static readonly Dictionary<string, string> Severity = new() { ["BUG-03"] = "Critical", ["BUG-01"] = "High", ["BUG-07"] = "Medium" };
    private static string? SeverityOf(string id) => Severity.GetValueOrDefault(id);

    private static LatestTestResult R(int test, int run, ScenarioStatus status, params string[] bugs) =>
        new(test, run, DateTimeOffset.UtcNow.AddMinutes(run), status, bugs);

    [Fact]
    public void All_tests_passing_is_GO()
    {
        var d = ReleaseVerdict.Decide([1, 2, 3], [R(1, 10, Passed), R(2, 10, Passed), R(3, 10, Passed)], SeverityOf);
        Assert.True(d.Go);
        Assert.Equal(100, d.PassRate);
    }

    [Fact]
    public void A_critical_or_high_known_bug_blocks_the_release()
    {
        var d = ReleaseVerdict.Decide([1, 2], [R(1, 10, Passed), R(2, 10, ExpectedFailure, "BUG-03")], SeverityOf);
        Assert.False(d.Go);
        Assert.Equal(["BUG-03"], d.BlockingBugIds);
        Assert.Equal(1, d.CriticalBugs);
    }

    [Fact]
    public void A_medium_known_bug_does_not_block()
    {
        Assert.True(ReleaseVerdict.Decide([1, 2], [R(1, 10, Passed), R(2, 10, ExpectedFailure, "BUG-07")], SeverityOf).Go);
    }

    [Fact]
    public void An_unexpected_failure_blocks_the_release()
    {
        var d = ReleaseVerdict.Decide([1, 2], [R(1, 10, Passed), R(2, 10, Failed)], SeverityOf);
        Assert.False(d.Go);
        Assert.Equal(1, d.UnexpectedFailures);
    }

    [Fact]
    public void A_newer_partial_run_updates_the_tests_it_executed()
    {
        // The caller passes the NEWEST result per test: run 20 re-ran test 2 and the bug is fixed (UnexpectedPass).
        var d = ReleaseVerdict.Decide([1, 2], [R(1, 16, Passed), R(2, 20, UnexpectedPass)], SeverityOf);
        Assert.True(d.Go);
        Assert.Equal([16, 20], d.SourceRunIds);
        Assert.Equal(20, d.NewestRunId);
    }

    [Fact]
    public void A_partial_run_that_passed_one_test_cannot_hide_the_others()
    {
        // Run 21 passed only test 1; test 2 still fails on BUG-01 from run 16 → still NO-GO.
        var d = ReleaseVerdict.Decide([1, 2], [R(1, 21, Passed), R(2, 16, ExpectedFailure, "BUG-01")], SeverityOf);
        Assert.False(d.Go);
        Assert.Equal(["BUG-01"], d.BlockingBugIds);
    }

    [Fact]
    public void A_suite_test_that_never_ran_prevents_GO()
    {
        var d = ReleaseVerdict.Decide([1, 2, 3], [R(1, 10, Passed), R(2, 10, Passed)], SeverityOf);
        Assert.False(d.Go);
        Assert.Equal(1, d.TestsNeverRun);
    }

    [Fact]
    public void Results_of_tests_outside_the_current_suite_are_ignored()
    {
        // Test 99 was removed from the code; its old failure must not block forever.
        Assert.True(ReleaseVerdict.Decide([1], [R(1, 10, Passed), R(99, 3, Failed)], SeverityOf).Go);
    }

    [Fact]
    public void An_empty_suite_is_never_GO()
    {
        Assert.False(ReleaseVerdict.Decide([], [], SeverityOf).Go);
    }
}
