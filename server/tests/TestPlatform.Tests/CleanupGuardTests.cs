using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TestPlatform.Api.Contracts;
using TestPlatform.Api.Domain;
using TestPlatform.Api.Hubs;
using TestPlatform.Api.Services;
using static TestPlatform.Api.Domain.CleanupGuard;

namespace TestPlatform.Tests;

/// <summary>B-06 — TM-06: "Clean test data" must never touch seeded data or a record that is not ours.</summary>
public class CleanupGuardTests
{
    private static readonly IReadOnlySet<string> Seeded = ParseProtectedIds("1-11");

    private static TestDataRecord Offender(string id, string nationalId = "990000001", TestDataState state = TestDataState.Active) =>
        new() { Kind = TestDataKind.Offender, ExternalId = id, NaturalKey = nationalId, State = state };

    [Theory]
    [InlineData("1")]
    [InlineData("6")]
    [InlineData("11")]
    public void Seeded_ids_are_never_deleted_even_when_everything_else_matches(string seededId)
    {
        Assert.Equal(Decision.SkipProtected, Decide(Offender(seededId), Seeded, liveNaturalKey: "990000001", liveRecordExists: true));
    }

    [Fact]
    public void An_active_registered_record_with_matching_identity_is_deleted()
    {
        Assert.Equal(Decision.Delete, Decide(Offender("512"), Seeded, "990000001", true));
    }

    [Fact]
    public void A_reused_id_that_now_belongs_to_someone_else_is_not_deleted()
    {
        Assert.Equal(Decision.SkipIdentityMismatch, Decide(Offender("512"), Seeded, "305412876", true));
    }

    [Fact]
    public void A_record_without_registered_identity_is_not_deleted()
    {
        var record = Offender("512");
        record.NaturalKey = null;
        Assert.Equal(Decision.SkipIdentityMismatch, Decide(record, Seeded, "990000001", true));
    }

    [Theory]
    [InlineData(TestDataState.DeletedByTest)]
    [InlineData(TestDataState.CleanedByPlatform)]
    [InlineData(TestDataState.Protected)]
    public void Records_that_are_no_longer_active_are_skipped(TestDataState state)
    {
        Assert.Equal(Decision.SkipNotActive, Decide(Offender("512", state: state), Seeded, "990000001", true));
    }

    [Fact]
    public void A_record_whose_previous_clean_up_failed_is_retried()
    {
        Assert.Equal(Decision.Delete, Decide(Offender("512", state: TestDataState.CleanupFailed), Seeded, "990000001", true));
    }

    [Fact]
    public void A_record_already_gone_from_the_application_is_not_deleted_again()
    {
        Assert.Equal(Decision.SkipAlreadyGone, Decide(Offender("512"), Seeded, null, liveRecordExists: false));
    }

    [Fact]
    public void Location_points_are_never_deleted_directly()
    {
        var point = new TestDataRecord { Kind = TestDataKind.LocationPoint, ExternalId = "900", State = TestDataState.Active };
        Assert.Equal(Decision.SkipNotDeletable, Decide(point, Seeded, null, true));
    }

    [Theory]
    [InlineData("1-11", 11)]
    [InlineData("1-3,7", 4)]
    [InlineData(" 5 ", 1)]
    [InlineData("", 0)]
    public void Protected_id_spec_is_parsed(string spec, int expectedCount)
    {
        Assert.Equal(expectedCount, ParseProtectedIds(spec).Count);
    }

    [Fact]
    public void An_invalid_protected_id_spec_is_rejected()
    {
        Assert.Throws<FormatException>(() => ParseProtectedIds("1-11,abc"));
    }

    /// <summary>
    /// End to end through TestDataService with a real (in-memory) database and a fake OffenderWatch API:
    /// a seeded id registered by a scenario, a normal test record, a re-used id and a vanished record.
    /// Only the normal test record may receive a DELETE.
    /// </summary>
    [Fact]
    public async Task Clean_test_data_deletes_only_our_own_records_and_never_seeded_ones()
    {
        using var database = new TestDatabase();
        await using var db = database.NewContext();
        var env = new TargetEnvironment { Name = "Test", BaseUrl = "https://app.example/", ProtectedIds = "1-11", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.Environments.Add(env);
        var run = new Run { EnvironmentId = 0, EnvironmentName = "Test", EnvironmentBaseUrl = env.BaseUrl, Status = RunStatus.Running, CreatedAt = DateTimeOffset.UtcNow };
        db.Runs.Add(run);
        await db.SaveChangesAsync();
        run.EnvironmentId = env.Id;
        await db.SaveChangesAsync();

        var target = new FakeTargetApi();
        target.Responses["GET /api/offenders/3"] = (HttpStatusCode.OK, """{"id":3,"nationalId":"301287465"}""");
        target.Responses["GET /api/offenders/600"] = (HttpStatusCode.OK, """{"id":600,"nationalId":"990000600"}""");
        target.Responses["DELETE /api/offenders/600"] = (HttpStatusCode.NoContent, "");
        target.Responses["GET /api/offenders/601"] = (HttpStatusCode.OK, """{"id":601,"nationalId":"123456789"}""");
        target.Responses["GET /api/offenders/602"] = (HttpStatusCode.OK, ""); // the app's "200 + empty body" for unknown ids (BUG-08)
        // After deletion: 600's point is STILL returned (BUG-02 — no cascade); 602's trail is really empty.
        target.Responses["GET /api/offenders/600/trail"] = (HttpStatusCode.OK, """[{"id":901,"offenderId":600}]""");
        target.Responses["GET /api/offenders/602/trail"] = (HttpStatusCode.OK, "[]");

        var service = new TestDataService(db, target, new RunEvents(new NullHubContext()), NullLogger<TestDataService>.Instance);
        await service.RegisterAsync(run.Id, new IngestTestDataInput("Offender", "3", "301287465", null, "Seeded Peretz", null), default);
        await service.RegisterAsync(run.Id, new IngestTestDataInput("Offender", "600", "990000600", null, "Ours", null), default);
        await service.RegisterAsync(run.Id, new IngestTestDataInput("Offender", "601", "990000601", null, "Re-used id", null), default);
        await service.RegisterAsync(run.Id, new IngestTestDataInput("Offender", "602", "990000602", null, "Vanished", null), default);
        await service.RegisterAsync(run.Id, new IngestTestDataInput("LocationPoint", "901", null, "600", "Point of 600", null), default);
        await service.RegisterAsync(run.Id, new IngestTestDataInput("LocationPoint", "902", null, "602", "Point of 602", null), default);

        var report = await service.CleanAsync(env.Id, default);

        Assert.Equal(new[] { "DELETE /api/offenders/600" }, target.Calls.Where(c => c.StartsWith("DELETE")));
        Assert.DoesNotContain("GET /api/offenders/3", target.Calls); // seeded: protected at registration, never even looked up
        Assert.Equal(1, report.Deleted);
        var states = db.TestData.ToDictionary(t => t.ExternalId, t => t.State);
        Assert.Equal(TestDataState.Protected, states["3"]);
        Assert.Equal(TestDataState.CleanedByPlatform, states["600"]);
        Assert.Equal(TestDataState.IdentityChanged, states["601"]); // the id belongs to someone else → left untouched
        Assert.Equal(TestDataState.AlreadyGone, states["602"]);
        // Point states are VERIFIED against the application, never assumed:
        Assert.Equal(TestDataState.OrphanedInApplication, states["901"]); // still returned after its offender was deleted
        Assert.Equal(TestDataState.RemovedWithParent, states["902"]);     // really gone
    }
}

/// <summary>TM-08 — evidence can never be served as something the browser would execute.</summary>
public class EvidenceContentTypeTests
{
    [Theory]
    [InlineData("image/png", "image/png")]
    [InlineData("text/plain; charset=utf-8", "text/plain")]
    [InlineData("application/json", "application/json")]
    [InlineData("application/zip", "application/zip")]
    [InlineData("text/html", "application/octet-stream")]
    [InlineData("image/svg+xml", "application/octet-stream")]   // SVG can carry script
    [InlineData("application/javascript", "application/octet-stream")]
    [InlineData(null, "application/octet-stream")]
    public void Only_inert_content_types_are_kept(string? declared, string expected)
    {
        Assert.Equal(expected, TestPlatform.Api.Services.EvidenceStore.SafeContentType(declared));
    }
}
