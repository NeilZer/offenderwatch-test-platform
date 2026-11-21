using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TestPlatform.Api.Contracts;
using TestPlatform.Api.Data;
using TestPlatform.Api.Domain;
using TestPlatform.Api.Hubs;
using TestPlatform.Api.Infrastructure;

namespace TestPlatform.Api.Services;

/// <summary>
/// TM-06 — test data created by scenarios, attributed to the run that created it, and the
/// "Clean test data" action that deletes it from the target environment through the application's
/// own API. Seeded data is protected by <see cref="CleanupGuard"/>.
/// </summary>
public class TestDataService(PlatformDbContext db, IHttpClientFactory http, RunEvents events, ILogger<TestDataService> logger)
{
    public async Task<TestDataRecord> RegisterAsync(int runId, IngestTestDataInput input, CancellationToken ct)
    {
        var run = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct) ?? throw new NotFoundException($"Run {runId} not found");
        if (run.FinishedAt is not null) throw new ConflictException($"Run {runId} is finished — data can only be registered while it runs");
        var kind = ParseKind(input.Kind);
        var record = new TestDataRecord
        {
            RunId = runId,
            EnvironmentId = run.EnvironmentId,
            EnvironmentBaseUrl = run.EnvironmentBaseUrl,
            ScenarioTitle = input.ScenarioTitle,
            Kind = kind,
            ExternalId = input.ExternalId,
            NaturalKey = input.NaturalKey,
            ParentExternalId = input.ParentExternalId,
            Label = string.IsNullOrWhiteSpace(input.Label) ? $"{kind} {input.ExternalId}" : input.Label,
            State = TestDataState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var protectedIds = await ProtectedIdsAsync(run.EnvironmentId, ct);
        if (kind == TestDataKind.Offender && protectedIds.Contains(record.ExternalId))
        {
            // A scenario must never be able to mark seeded data as "its own" — it could then be cleaned.
            record.State = TestDataState.Protected;
            record.ResolvedAt = record.CreatedAt;
            record.Note = "Seeded record — protected, will never be deleted by the platform.";
        }
        db.TestData.Add(record);
        await db.SaveChangesAsync(ct);
        await events.TestDataChanged();
        return record;
    }

    /// <summary>The scenario deleted its own data (normal teardown).</summary>
    public async Task ReleaseAsync(int runId, IngestReleaseInput input, CancellationToken ct)
    {
        var kind = ParseKind(input.Kind);
        var baseUrl = await db.Runs.Where(r => r.Id == runId).Select(r => r.EnvironmentBaseUrl).FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException($"Run {runId} not found");
        var record = await db.TestData
            .Where(t => t.Kind == kind && t.ExternalId == input.ExternalId && t.EnvironmentBaseUrl == baseUrl && t.State == TestDataState.Active)
            .OrderByDescending(t => t.Id).FirstOrDefaultAsync(ct);
        if (record is null) return;
        var state = Enum.TryParse<TestDataState>(input.State, true, out var s) ? s : TestDataState.DeletedByTest;
        Resolve(record, state, input.Note ?? "Deleted by the scenario's own teardown.");
        await ResolveChildrenAsync(record, ct);
        await db.SaveChangesAsync(ct);
        await events.TestDataChanged();
    }

    public async Task<IReadOnlyList<TestDataDto>> ListAsync(int? environmentId, bool onlyActive, CancellationToken ct)
    {
        var query = db.TestData.AsNoTracking();
        if (environmentId is not null) query = query.Where(t => t.EnvironmentId == environmentId);
        if (onlyActive) query = query.Where(t => t.State == TestDataState.Active);
        var records = await query.OrderByDescending(t => t.Id).Take(1000).ToListAsync(ct);
        var names = await db.Environments.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.Name, ct);
        return records.Select(r => r.ToDto(r.EnvironmentId is { } id && names.TryGetValue(id, out var n) ? n : null)).ToList();
    }

    /// <summary>"Clean test data": delete every still-active record of one environment through the app's API.</summary>
    public async Task<CleanupReportDto> CleanAsync(int environmentId, CancellationToken ct)
    {
        var env = await db.Environments.AsNoTracking().FirstOrDefaultAsync(e => e.Id == environmentId, ct)
                  ?? throw new NotFoundException($"Environment {environmentId} not found");
        var protectedIds = CleanupGuard.ParseProtectedIds(env.ProtectedIds);
        var records = await db.TestData
            .Where(t => t.EnvironmentBaseUrl == env.BaseUrl && t.Kind == TestDataKind.Offender
                        && (t.State == TestDataState.Active || t.State == TestDataState.CleanupFailed)) // failed attempts are retried
            .OrderBy(t => t.Id).ToListAsync(ct);

        var client = http.CreateClient("target");
        client.BaseAddress = new Uri(env.BaseUrl.EndsWith('/') ? env.BaseUrl : env.BaseUrl + "/");
        int deleted = 0, gone = 0, prot = 0, skipped = 0, failed = 0;
        var details = new List<string>();

        foreach (var record in records)
        {
            try
            {
                var (exists, liveNationalId) = await ReadLiveOffenderAsync(client, record.ExternalId, ct);
                switch (CleanupGuard.Decide(record, protectedIds, liveNationalId, exists))
                {
                    case CleanupGuard.Decision.Delete:
                        var response = await client.DeleteAsync($"api/offenders/{record.ExternalId}", ct);
                        if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK)
                        {
                            Resolve(record, TestDataState.CleanedByPlatform, "Deleted by 'Clean test data'.");
                            deleted++;
                        }
                        else if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            Resolve(record, TestDataState.AlreadyGone, "Already deleted in the application.");
                            gone++;
                        }
                        else
                        {
                            record.Note = $"Delete failed: HTTP {(int)response.StatusCode}";
                            record.State = TestDataState.CleanupFailed;
                            failed++;
                        }
                        break;
                    case CleanupGuard.Decision.SkipAlreadyGone:
                        Resolve(record, TestDataState.AlreadyGone, "Not found in the application any more.");
                        gone++;
                        break;
                    case CleanupGuard.Decision.SkipProtected:
                        Resolve(record, TestDataState.Protected, "Seeded id — protected.");
                        prot++;
                        break;
                    case CleanupGuard.Decision.SkipIdentityMismatch:
                        Resolve(record, TestDataState.AlreadyGone, $"Id now belongs to a different record (national ID {liveNationalId}) — NOT deleted.");
                        skipped++;
                        details.Add($"Offender {record.ExternalId}: identity changed, left untouched");
                        break;
                    default:
                        skipped++;
                        break;
                }
                await ResolveChildrenAsync(record, ct);
            }
            catch (HttpRequestException ex)
            {
                failed++;
                record.State = TestDataState.CleanupFailed;
                record.Note = $"Target unreachable: {ex.Message}";
                logger.LogWarning(ex, "Clean-up of offender {Id} failed", record.ExternalId);
            }
        }
        await db.SaveChangesAsync(ct);
        await events.TestDataChanged();
        details.Insert(0, $"{env.Name}: examined {records.Count}, deleted {deleted}, already gone {gone}, protected {prot}, skipped {skipped}, failed {failed}.");
        return new CleanupReportDto(records.Count, deleted, gone, prot, skipped, failed, details);
    }

    /// <summary>
    /// Reads the live record. NOTE: the application returns 200 with an EMPTY body for unknown ids
    /// (BUG-08), so "exists" is decided by the body, not by the status code.
    /// </summary>
    private static async Task<(bool Exists, string? NationalId)> ReadLiveOffenderAsync(HttpClient client, string id, CancellationToken ct)
    {
        var response = await client.GetAsync($"api/offenders/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return (false, null);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body)) return (false, null);
        using var json = JsonDocument.Parse(body);
        return json.RootElement.TryGetProperty("nationalId", out var nid) ? (true, nid.GetString()) : (true, null);
    }

    /// <summary>
    /// After an offender is deleted, decide what happened to its location points — by ASKING the application,
    /// never by assumption: points the trail still returns are "left in the application" (BUG-02 — the app
    /// does not cascade, and has no endpoint to delete a single point); only points that are really gone are
    /// "removed with their offender". If the check itself fails, the points are reported as left (the safe,
    /// observed default under BUG-02) with the reason in the note.
    /// </summary>
    private async Task ResolveChildrenAsync(TestDataRecord parent, CancellationToken ct)
    {
        if (parent.Kind != TestDataKind.Offender || CleanupGuard.IsWaitingForCleanup(parent.State)) return;
        var children = await db.TestData
            .Where(t => t.Kind == TestDataKind.LocationPoint && t.ParentExternalId == parent.ExternalId
                        && t.EnvironmentBaseUrl == parent.EnvironmentBaseUrl && t.State == TestDataState.Active)
            .ToListAsync(ct);
        if (children.Count == 0) return;

        var (stillPresent, checkError) = await ReadTrailPointIdsAsync(parent.EnvironmentBaseUrl, parent.ExternalId, ct);
        foreach (var child in children)
        {
            if (checkError is null && !stillPresent.Contains(child.ExternalId))
            {
                Resolve(child, TestDataState.RemovedWithParent, "Verified: no longer returned by the application after its offender was deleted.");
            }
            else
            {
                Resolve(child, TestDataState.OrphanedInApplication, checkError is null
                    ? "Verified: still returned by the application after its offender was deleted (BUG-02). The app has no endpoint to delete a single point."
                    : $"Assumed still in the application (BUG-02) — the check failed: {checkError}");
            }
        }
    }

    /// <summary>Ids of the points the application still returns for an offender (empty when the trail is gone).</summary>
    private async Task<(IReadOnlySet<string> Ids, string? Error)> ReadTrailPointIdsAsync(string baseUrl, string offenderId, CancellationToken ct)
    {
        try
        {
            var client = http.CreateClient("target");
            client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
            var response = await client.GetAsync($"api/offenders/{offenderId}/trail", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return (new HashSet<string>(), null);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body)) return (new HashSet<string>(), null);
            using var json = JsonDocument.Parse(body);
            var ids = json.RootElement.ValueKind == JsonValueKind.Array
                ? json.RootElement.EnumerateArray().Select(p => p.TryGetProperty("id", out var id) ? id.ToString() : "").ToHashSet()
                : new HashSet<string>();
            return (ids, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Could not verify the trail of offender {Id}", offenderId);
            return (new HashSet<string>(), ex.Message);
        }
    }

    private async Task<IReadOnlySet<string>> ProtectedIdsAsync(int? environmentId, CancellationToken ct)
    {
        var spec = environmentId is null ? null : await db.Environments.Where(e => e.Id == environmentId).Select(e => e.ProtectedIds).FirstOrDefaultAsync(ct);
        return CleanupGuard.ParseProtectedIds(spec ?? "1-11");
    }

    private static void Resolve(TestDataRecord record, TestDataState state, string note)
    {
        record.State = state;
        record.ResolvedAt = DateTimeOffset.UtcNow;
        record.Note = note;
    }

    private static TestDataKind ParseKind(string kind) =>
        Enum.TryParse<TestDataKind>(kind, true, out var k) ? k : throw new BadRequestException($"Unknown test-data kind '{kind}'");
}
