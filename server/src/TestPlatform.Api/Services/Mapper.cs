using TestPlatform.Api.Contracts;
using TestPlatform.Api.Domain;

namespace TestPlatform.Api.Services;

/// <summary>Entity → DTO mapping in one place.</summary>
public static class Mapper
{
    public static EnvironmentDto ToDto(this TargetEnvironment e) =>
        new(e.Id, e.Name, e.BaseUrl, e.IsDefault, e.ProtectedIds, e.CreatedAt, e.UpdatedAt);

    public static RunSummaryDto ToSummary(this Run r) =>
        new(r.Id, r.EnvironmentId, r.EnvironmentName, r.EnvironmentBaseUrl, r.Trigger.ToString(), r.Status.ToString(), r.Scope, r.Filter, r.IsFullSuite,
            r.StartedBy, r.CreatedAt, r.StartedAt, r.FinishedAt,
            r.DurationMs ?? (r.StartedAt is { } s && r.FinishedAt is null ? (long)(DateTimeOffset.UtcNow - s).TotalMilliseconds : null),
            new RunTotals(r.Total, r.Passed, r.Failed, r.ExpectedFailures, r.UnexpectedPasses, r.Skipped),
            r.Passed + r.Failed + r.ExpectedFailures + r.UnexpectedPasses + r.Skipped,
            r.StatusReason);

    public static EvidenceDto ToDto(this Evidence e) =>
        new(e.Id, e.Kind.ToString(), e.FileName, e.ContentType, e.SizeBytes, e.Sha256, e.CreatedAt, $"/api/evidence/{e.Id}");

    public static ScenarioDto ToDto(this ScenarioResult s, IReadOnlyDictionary<string, KnownBug> bugs) =>
        new(s.Id, s.RunId, s.TestCaseId, s.TestCase?.Title ?? "", s.TestCase?.File ?? "", s.TestCase?.Project ?? "", s.TestCase?.Tags ?? "",
            s.Ordinal, s.Status.ToString(), s.StartedAt, s.FinishedAt, s.DurationMs, s.ErrorMessage, s.ErrorStack,
            ResolveBugs(s.KnownBugs, bugs), s.Evidence.OrderBy(e => e.Id).Select(e => e.ToDto()).ToList());

    public static IReadOnlyList<KnownBugDto> ResolveBugs(string? ids, IReadOnlyDictionary<string, KnownBug> bugs) =>
        string.IsNullOrWhiteSpace(ids)
            ? Array.Empty<KnownBugDto>()
            : ids.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => bugs.TryGetValue(id, out var b) ? new KnownBugDto(b.Id, b.Title, b.Severity) : new KnownBugDto(id, "", null))
                .ToList();

    public static TestDataDto ToDto(this TestDataRecord t, string? environmentName) =>
        new(t.Id, t.RunId, t.EnvironmentBaseUrl, environmentName, t.ScenarioTitle, t.Kind.ToString(), t.ExternalId, t.NaturalKey,
            t.ParentExternalId, t.Label, t.State.ToString(), t.CreatedAt, t.ResolvedAt, t.Note);

    public static ScheduleDto ToDto(this Schedule s) =>
        new(s.Id, s.Name, s.Cron, s.TimeZone, s.EnvironmentId, s.Environment?.Name, s.Scope, s.Enabled, s.NextRunAt, s.LastRunAt, s.LastRunId);

    public static NotificationDto ToDto(this Notification n) =>
        new(n.Id, n.RunId, n.CreatedAt, n.Summary, n.Target, n.Delivered, n.DeliveryError);

    public static EnvironmentSummaryDto ToDto(this EnvironmentSummary s) =>
        new(s.EnvironmentId, s.EnvironmentName, s.Current.ToString(), s.CurrentStatus?.ToString(), s.LastPassedAt, s.FailingSince,
            s.FailingSinceRunId, s.IsFlaky, s.Flips, s.Executions);
}
