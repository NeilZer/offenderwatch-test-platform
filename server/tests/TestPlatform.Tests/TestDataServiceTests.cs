using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TestPlatform.Api.Contracts;
using TestPlatform.Api.Data;
using TestPlatform.Api.Domain;
using TestPlatform.Api.Hubs;
using TestPlatform.Api.Services;

namespace TestPlatform.Tests;

/// <summary>
/// TM-06 — what the platform says about location points after their offender is deleted must be PROVEN by
/// the application's reply. Anything the reply cannot prove is "Unverified", never "verified".
/// </summary>
public sealed class TestDataServiceTests : IDisposable
{
    private const string BaseUrl = "https://app.example/";
    private readonly TestDatabase database = new();
    private readonly PlatformDbContext db;
    private readonly FakeTargetApi target = new();
    private readonly TestDataService service;
    private readonly Run run;
    private readonly TargetEnvironment env;

    public TestDataServiceTests()
    {
        db = database.NewContext();
        env = new TargetEnvironment { Name = "Test", BaseUrl = BaseUrl, ProtectedIds = "1-11", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.Environments.Add(env);
        db.SaveChanges();
        run = new Run { EnvironmentId = env.Id, EnvironmentName = "Test", EnvironmentBaseUrl = BaseUrl, Status = RunStatus.Running, CreatedAt = DateTimeOffset.UtcNow };
        db.Runs.Add(run);
        db.SaveChanges();
        service = new TestDataService(db, target, new RunEvents(new NullHubContext()), NullLogger<TestDataService>.Instance);
    }

    /// <summary>Offender 600 (ours) with one point 901; the test deletes the offender itself.</summary>
    private async Task<TestDataState> PointStateAfterTestDeletesOffenderAsync()
    {
        await service.RegisterAsync(run.Id, new IngestTestDataInput("Offender", "600", "990000600", null, "Ours", null), default);
        await service.RegisterAsync(run.Id, new IngestTestDataInput("LocationPoint", "901", null, "600", "Point", null), default);
        await service.ReleaseAsync(run.Id, new IngestReleaseInput("Offender", "600", "DeletedByTest", null), default);
        return db.TestData.Single(t => t.ExternalId == "901").State;
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{}")]                        // valid JSON, but not a list of points
    [InlineData(HttpStatusCode.OK, "")]                          // empty body
    [InlineData(HttpStatusCode.OK, """[{"lat":1}]""")]           // an entry without an id
    [InlineData(HttpStatusCode.OK, "not json")]                  // unreadable
    [InlineData(HttpStatusCode.InternalServerError, "[]")]       // the request failed
    public async Task A_reply_that_proves_nothing_is_Unverified_never_removed(HttpStatusCode status, string body)
    {
        target.Responses["GET /api/offenders/600/trail"] = (status, body);
        Assert.Equal(TestDataState.Unverified, await PointStateAfterTestDeletesOffenderAsync());
        Assert.StartsWith("Not verified", db.TestData.Single(t => t.ExternalId == "901").Note);
    }

    [Fact]
    public async Task A_point_still_returned_is_verified_as_left_in_the_application()
    {
        target.Responses["GET /api/offenders/600/trail"] = (HttpStatusCode.OK, """[{"id":901,"offenderId":600}]""");
        Assert.Equal(TestDataState.OrphanedInApplication, await PointStateAfterTestDeletesOffenderAsync());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "[]")]                        // an empty list of points
    [InlineData(HttpStatusCode.NotFound, "")]                    // the trail no longer exists
    public async Task A_point_proven_gone_is_verified_as_removed(HttpStatusCode status, string body)
    {
        target.Responses["GET /api/offenders/600/trail"] = (status, body);
        Assert.Equal(TestDataState.RemovedWithParent, await PointStateAfterTestDeletesOffenderAsync());
    }

    [Fact]
    public async Task Points_of_a_protected_parent_are_never_touched()
    {
        // A record with a seeded id that is still Active (e.g. registered before protection existed).
        db.TestData.AddRange(
            new TestDataRecord { RunId = run.Id, EnvironmentId = env.Id, EnvironmentBaseUrl = BaseUrl, Kind = TestDataKind.Offender, ExternalId = "3", NaturalKey = "301287465", State = TestDataState.Active, CreatedAt = DateTimeOffset.UtcNow },
            new TestDataRecord { RunId = run.Id, EnvironmentId = env.Id, EnvironmentBaseUrl = BaseUrl, Kind = TestDataKind.LocationPoint, ExternalId = "950", ParentExternalId = "3", State = TestDataState.Active, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        target.Responses["GET /api/offenders/3"] = (HttpStatusCode.OK, """{"id":3,"nationalId":"301287465"}""");

        await service.CleanAsync(env.Id, default);

        Assert.Equal(TestDataState.Protected, db.TestData.Single(t => t.ExternalId == "3").State);
        Assert.Equal(TestDataState.Active, db.TestData.Single(t => t.ExternalId == "950").State); // the offender was not deleted
        Assert.DoesNotContain(target.Calls, c => c.StartsWith("DELETE") || c.Contains("/trail"));
    }

    [Fact]
    public async Task Points_under_a_reused_id_are_Unverified_and_the_parent_is_IdentityChanged()
    {
        await service.RegisterAsync(run.Id, new IngestTestDataInput("Offender", "601", "990000601", null, "Ours", null), default);
        await service.RegisterAsync(run.Id, new IngestTestDataInput("LocationPoint", "902", null, "601", "Point", null), default);
        target.Responses["GET /api/offenders/601"] = (HttpStatusCode.OK, """{"id":601,"nationalId":"123456789"}"""); // someone else now

        await service.CleanAsync(env.Id, default);

        Assert.Equal(TestDataState.IdentityChanged, db.TestData.Single(t => t.ExternalId == "601").State);
        Assert.Equal(TestDataState.Unverified, db.TestData.Single(t => t.ExternalId == "902").State);
        Assert.DoesNotContain(target.Calls, c => c.StartsWith("DELETE") || c.Contains("/trail")); // their trail proves nothing
    }

    [Fact]
    public async Task Still_in_the_application_filter_includes_orphans_failures_and_unverified()
    {
        foreach (var state in Enum.GetValues<TestDataState>())
        {
            db.TestData.Add(new TestDataRecord { RunId = run.Id, EnvironmentId = env.Id, EnvironmentBaseUrl = BaseUrl, Kind = TestDataKind.LocationPoint, ExternalId = state.ToString(), State = state, CreatedAt = DateTimeOffset.UtcNow });
        }
        await db.SaveChangesAsync();

        var shown = (await service.ListAsync(env.Id, stillInApplication: true, default)).Select(r => r.State).ToHashSet();

        Assert.Equal(new HashSet<string> { "Active", "CleanupFailed", "OrphanedInApplication", "Unverified" }, shown);
    }

    public void Dispose()
    {
        db.Dispose();
        database.Dispose();
    }
}
