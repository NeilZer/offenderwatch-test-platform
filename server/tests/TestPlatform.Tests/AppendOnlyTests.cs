using Microsoft.EntityFrameworkCore;
using TestPlatform.Api.Domain;

namespace TestPlatform.Tests;

/// <summary>B-06 — TM-05: history is append-only, enforced by the database triggers from the migrations.</summary>
public class AppendOnlyTests
{
    private static async Task<(PlatformDb Db, Run Run, ScenarioResult Result)> SeedFinishedRunAsync(TestDatabase database)
    {
        var db = database.NewContext();
        var testCase = new TestCase { Key = "api › file › t", Title = "t", FirstSeenAt = DateTimeOffset.UtcNow };
        var run = new Run { EnvironmentName = "env", EnvironmentBaseUrl = "https://x/", Status = RunStatus.Running, CreatedAt = DateTimeOffset.UtcNow };
        var result = new ScenarioResult { Run = run, TestCase = testCase, Status = ScenarioStatus.Running, Ordinal = 1 };
        db.AddRange(testCase, run, result);
        await db.SaveChangesAsync();

        // Finishing is allowed (row still open)…
        result.Status = ScenarioStatus.Failed;
        result.FinishedAt = DateTimeOffset.UtcNow;
        run.Status = RunStatus.Completed;
        run.FinishedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return (new PlatformDb(db), run, result);
    }

    private sealed record PlatformDb(TestPlatform.Api.Data.PlatformDbContext Context);

    [Fact]
    public async Task A_finished_result_cannot_be_rewritten()
    {
        using var database = new TestDatabase();
        var (db, _, result) = await SeedFinishedRunAsync(database);
        result.Status = ScenarioStatus.Passed;
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.Context.SaveChangesAsync());
        Assert.Contains("Append-only", error.InnerException!.Message);
    }

    [Fact]
    public async Task A_finished_run_cannot_be_changed_or_deleted()
    {
        using var database = new TestDatabase();
        var (db, run, _) = await SeedFinishedRunAsync(database);
        run.Passed = 999;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.Context.SaveChangesAsync());

        await using var fresh = database.NewContext();
        var sql = $"DELETE FROM Runs WHERE Id = {run.Id}";
        await Assert.ThrowsAnyAsync<Exception>(() => fresh.Database.ExecuteSqlRawAsync(sql));
    }

    [Fact]
    public async Task Results_and_evidence_cannot_be_deleted()
    {
        using var database = new TestDatabase();
        var (db, _, result) = await SeedFinishedRunAsync(database);
        db.Context.Evidence.Add(new Evidence { ScenarioResultId = result.Id, Kind = EvidenceKind.Log, FileName = "log", ContentType = "text/plain", RelativePath = "x", Sha256 = "abc", CreatedAt = DateTimeOffset.UtcNow });
        await db.Context.SaveChangesAsync();

        await using var fresh = database.NewContext();
        await Assert.ThrowsAnyAsync<Exception>(() => fresh.Database.ExecuteSqlRawAsync("DELETE FROM Evidence"));
        await Assert.ThrowsAnyAsync<Exception>(() => fresh.Database.ExecuteSqlRawAsync("UPDATE Evidence SET Sha256 = 'tampered'"));
        await Assert.ThrowsAnyAsync<Exception>(() => fresh.Database.ExecuteSqlRawAsync("DELETE FROM ScenarioResults"));
    }

    [Fact]
    public async Task A_new_run_never_changes_the_results_of_a_previous_run()
    {
        using var database = new TestDatabase();
        var (db, firstRun, firstResult) = await SeedFinishedRunAsync(database);
        var secondRun = new Run { EnvironmentName = "env", EnvironmentBaseUrl = "https://x/", Status = RunStatus.Running, CreatedAt = DateTimeOffset.UtcNow };
        db.Context.Runs.Add(secondRun);
        db.Context.ScenarioResults.Add(new ScenarioResult { Run = secondRun, TestCaseId = firstResult.TestCaseId, Status = ScenarioStatus.Passed, Ordinal = 1, FinishedAt = DateTimeOffset.UtcNow });
        await db.Context.SaveChangesAsync();

        await using var fresh = database.NewContext();
        var stored = await fresh.ScenarioResults.SingleAsync(r => r.RunId == firstRun.Id);
        Assert.Equal(ScenarioStatus.Failed, stored.Status);
        Assert.Equal(2, await fresh.ScenarioResults.CountAsync());
    }
}
