using Microsoft.Extensions.Logging.Abstractions;
using TestPlatform.Api.Data;
using TestPlatform.Api.Domain;
using TestPlatform.Api.Services;

namespace TestPlatform.Tests;

/// <summary>B-03 — only NEW unexpected failures notify; documented bugs never do.</summary>
public class RegressionNotifierTests
{
    private static int runCounter;

    private static async Task<Run> AddRunAsync(PlatformDbContext db, int environmentId, params (TestCase Test, ScenarioStatus Status)[] results)
    {
        var run = new Run
        {
            EnvironmentId = environmentId, EnvironmentName = $"env{environmentId}", EnvironmentBaseUrl = "https://x/",
            Status = RunStatus.Running, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(++runCounter),
        };
        db.Runs.Add(run);
        foreach (var (test, status) in results)
            db.ScenarioResults.Add(new ScenarioResult { Run = run, TestCase = test, Status = status, Ordinal = 1, FinishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        run.Status = RunStatus.Completed;
        run.FinishedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return run;
    }

    private static TestCase Test(string title) => new() { Key = title, Title = title, FirstSeenAt = DateTimeOffset.UtcNow };

    [Fact]
    public async Task Passed_then_failed_unexpectedly_notifies_but_a_known_bug_does_not()
    {
        using var database = new TestDatabase();
        await using var db = database.NewContext();
        var regressed = Test("TC-39 last seen");
        var knownBug = Test("TC-38 latest reading");
        var stillPassing = Test("TC-35 markers");
        await AddRunAsync(db, 1, (regressed, ScenarioStatus.Passed), (knownBug, ScenarioStatus.Passed), (stillPassing, ScenarioStatus.Passed));
        var run = await AddRunAsync(db, 1, (regressed, ScenarioStatus.Failed), (knownBug, ScenarioStatus.ExpectedFailure), (stillPassing, ScenarioStatus.Passed));

        var notification = await new RegressionNotifier(db, new FakeTargetApi(), NullLogger<RegressionNotifier>.Instance).OnRunFinishedAsync(run.Id, default);

        Assert.NotNull(notification);
        Assert.Contains("TC-39 last seen", notification!.Payload);
        Assert.DoesNotContain("TC-38", notification.Payload);
        Assert.DoesNotContain("TC-35", notification.Payload);
        Assert.False(notification.Delivered); // no webhook configured → logged only
    }

    [Fact]
    public async Task A_run_in_between_that_did_not_execute_the_test_does_not_hide_the_regression()
    {
        using var database = new TestDatabase();
        await using var db = database.NewContext();
        var ui = Test("TC-39 last seen");
        var api = Test("TC-05 contract");
        await AddRunAsync(db, 1, (ui, ScenarioStatus.Passed), (api, ScenarioStatus.Passed));
        await AddRunAsync(db, 1, (api, ScenarioStatus.Passed));                         // API-only run
        var run = await AddRunAsync(db, 1, (ui, ScenarioStatus.Failed), (api, ScenarioStatus.Passed));

        var notifier = new RegressionNotifier(db, new FakeTargetApi(), NullLogger<RegressionNotifier>.Instance);

        Assert.Equal(new[] { "TC-39 last seen" }, await notifier.FindNewFailuresAsync(run, default));
    }

    [Fact]
    public async Task A_test_that_was_already_failing_does_not_notify_again()
    {
        using var database = new TestDatabase();
        await using var db = database.NewContext();
        var t = Test("TC-39 last seen");
        await AddRunAsync(db, 1, (t, ScenarioStatus.Failed));
        var run = await AddRunAsync(db, 1, (t, ScenarioStatus.Failed));

        Assert.Null(await new RegressionNotifier(db, new FakeTargetApi(), NullLogger<RegressionNotifier>.Instance).OnRunFinishedAsync(run.Id, default));
    }

    [Fact]
    public async Task Results_on_another_environment_are_not_compared()
    {
        using var database = new TestDatabase();
        await using var db = database.NewContext();
        var t = Test("TC-39 last seen");
        await AddRunAsync(db, 2, (t, ScenarioStatus.Passed));   // passed on a different environment
        var run = await AddRunAsync(db, 1, (t, ScenarioStatus.Failed));

        Assert.Empty(await new RegressionNotifier(db, new FakeTargetApi(), NullLogger<RegressionNotifier>.Instance).FindNewFailuresAsync(run, default));
    }
}
