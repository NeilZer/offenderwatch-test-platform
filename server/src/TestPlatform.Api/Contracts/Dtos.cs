using System.ComponentModel.DataAnnotations;
using TestPlatform.Api.Domain;

namespace TestPlatform.Api.Contracts;

// ---------------------------------------------------------------- environments (TM-01)
public record EnvironmentDto(int Id, string Name, string BaseUrl, bool IsDefault, string ProtectedIds, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record EnvironmentInput(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [Required, Url, StringLength(500)] string BaseUrl,
    bool IsDefault,
    [StringLength(200)] string? ProtectedIds);

// ---------------------------------------------------------------- runs (TM-02)
public record RunTotals(int Total, int Passed, int Failed, int ExpectedFailures, int UnexpectedPasses, int Skipped);

public record RunSummaryDto(
    int Id,
    int? EnvironmentId,
    string EnvironmentName,
    string EnvironmentBaseUrl,
    string Trigger,
    string Status,
    string Scope,
    string? Filter,
    bool IsFullSuite,
    string? StartedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    long? DurationMs,
    RunTotals Totals,
    int Completed,
    string? StatusReason);

public record EvidenceDto(int Id, string Kind, string FileName, string ContentType, long SizeBytes, string Sha256, DateTimeOffset CreatedAt, string Url);

public record ScenarioDto(
    int Id,
    int RunId,
    int TestCaseId,
    string Title,
    string File,
    string Project,
    string Tags,
    int Ordinal,
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    long? DurationMs,
    string? ErrorMessage,
    string? ErrorStack,
    IReadOnlyList<KnownBugDto> KnownBugs,
    IReadOnlyList<EvidenceDto> Evidence);

public record KnownBugDto(string Id, string Title, string? Severity);

public record RunDetailDto(RunSummaryDto Run, IReadOnlyList<ScenarioDto> Scenarios);

/// <param name="KeepTestData">Skip the scenarios' own teardown so the data waits for "Clean test data" (TM-06 demo).</param>
public record StartRunInput([Required] int EnvironmentId, string? Scope, string? Grep, bool KeepTestData = false);

// ---------------------------------------------------------------- history (TM-04, B-01)
public record TestCaseListItemDto(
    int Id,
    string Title,
    string File,
    string Project,
    string Tags,
    int Executions,
    IReadOnlyList<EnvironmentSummaryDto> Environments);

public record EnvironmentSummaryDto(
    int? EnvironmentId,
    string EnvironmentName,
    string Current,
    string? CurrentStatus,
    DateTimeOffset? LastPassedAt,
    DateTimeOffset? FailingSince,
    int? FailingSinceRunId,
    bool IsFlaky,
    int Flips,
    int Executions);

public record HistoryEntryDto(
    int ResultId,
    int RunId,
    int? EnvironmentId,
    string EnvironmentName,
    DateTimeOffset At,
    string Status,
    string Verdict,
    string Transition,
    long? DurationMs,
    string? ErrorMessage,
    IReadOnlyList<KnownBugDto> KnownBugs,
    string? SuiteVersion,
    int EvidenceCount);

public record TestHistoryDto(TestCaseListItemDto TestCase, IReadOnlyList<HistoryEntryDto> Entries, FlakinessRuleDto Rule);

public record FlakinessRuleDto(int Window, int MinFlips, string Description);

// ---------------------------------------------------------------- dashboard (TM-07)
public record DashboardDto(
    VerdictDto Verdict,
    IReadOnlyList<LatestRunDto> LatestRunPerEnvironment,
    IReadOnlyList<TrendPointDto> Trend,
    IReadOnlyList<FailingTestDto> CurrentlyFailing,
    IReadOnlyList<RunSummaryDto> ActiveRuns,
    IReadOnlyList<NotificationDto> RecentNotifications);

/// <summary>Headline run of one environment: its latest complete run, plus newer runs that were only partial.</summary>
public record LatestRunDto(RunSummaryDto Run, bool IsComplete, IReadOnlyList<int> NewerRunIds);

public record VerdictDto(bool Go, string Line, string? EnvironmentName, int? RunId, VerdictBasisDto? Basis = null);

/// <summary>What the verdict was computed from, and how it compares with the last complete run.</summary>
public record VerdictBasisDto(
    int TestsConsidered,
    int TestsWithResult,
    int TestsNeverRun,
    int Passing,
    int UnexpectedFailures,
    IReadOnlyList<string> BlockingBugIds,
    IReadOnlyList<int> SourceRunIds,
    int? BaselineRunId,
    int ImprovedSinceBaseline,
    int WorsenedSinceBaseline);

/// <summary>One completed run on the pass-rate trend. Partial runs are included but flagged (not comparable with full runs).</summary>
public record TrendPointDto(int RunId, int? EnvironmentId, string EnvironmentName, DateTimeOffset At, double PassRate, int Passed, int Executed,
    bool IsFullSuite, string Scope, string? Filter, int Skipped);

public record FailingTestDto(
    int TestCaseId,
    string Title,
    string Tags,
    string EnvironmentName,
    string Status,
    bool Expected,
    DateTimeOffset? FailingSince,
    int? FailingSinceRunId,
    DateTimeOffset? LastPassedAt,
    IReadOnlyList<KnownBugDto> KnownBugs,
    bool IsFlaky);

// ---------------------------------------------------------------- comparison (B-02)
public record RunComparisonDto(
    RunSummaryDto Base,
    RunSummaryDto Target,
    IReadOnlyList<ComparisonRowDto> Regressed,
    IReadOnlyList<ComparisonRowDto> Recovered,
    IReadOnlyList<ComparisonRowDto> StatusChanged,
    IReadOnlyList<ComparisonRowDto> New,
    IReadOnlyList<ComparisonRowDto> Missing,
    int Unchanged,
    RunTotals Delta);

public record ComparisonRowDto(int TestCaseId, string Title, string Project, string? BaseStatus, string? TargetStatus);

// ---------------------------------------------------------------- test data (TM-06)
public record TestDataDto(
    int Id,
    int RunId,
    string EnvironmentBaseUrl,
    string? EnvironmentName,
    string? ScenarioTitle,
    string Kind,
    string ExternalId,
    string? NaturalKey,
    string? ParentExternalId,
    string Label,
    string State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    string? Note);

public record CleanupInput([Required] int EnvironmentId);

public record CleanupReportDto(int Examined, int Deleted, int AlreadyGone, int Protected, int Skipped, int Failed, IReadOnlyList<string> Details);

// ---------------------------------------------------------------- schedules (B-04)
public record ScheduleDto(int Id, string Name, string Cron, string TimeZone, int EnvironmentId, string? EnvironmentName, string Scope, bool Enabled,
    DateTimeOffset? NextRunAt, DateTimeOffset? LastRunAt, int? LastRunId);

public record ScheduleInput(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [Required, StringLength(100)] string Cron,
    string? TimeZone,
    [Required] int EnvironmentId,
    string? Scope,
    bool Enabled);

// ---------------------------------------------------------------- notifications & settings (B-03)
public record NotificationDto(int Id, int RunId, DateTimeOffset CreatedAt, string Summary, string? Target, bool Delivered, string? DeliveryError);

public record SettingsDto(string? WebhookUrl);

// ---------------------------------------------------------------- auth (B-07)
public record LoginInput([Required] string Username, [Required] string Password);

public record UserDto(string Username, string Role, bool CanOperate);

// ---------------------------------------------------------------- ingestion contract (runner → platform)
/// <param name="Filter">Any test filter the runner applied (e.g. a grep). A filtered run never counts as a full-suite run.</param>
public record IngestCreateRunInput(string? EnvironmentBaseUrl, string? Trigger, string? SuiteVersion, string? Scope, string? Filter = null);

public record IngestScenarioInput([Required] string Key, [Required] string Title, string? File, string? Project, IReadOnlyList<string>? Tags);

public record IngestBeginInput(string? SuiteVersion, IReadOnlyList<IngestScenarioInput> Scenarios);

public record IngestBeginResult(int RunId, IReadOnlyDictionary<string, int> ResultIdsByKey);

public record IngestFinishInput(
    [Required] string Status,
    long? DurationMs,
    string? ErrorMessage,
    string? ErrorStack,
    IReadOnlyList<KnownBugDto>? KnownBugs);

public record IngestTestDataInput(
    [Required] string Kind,
    [Required] string ExternalId,
    string? NaturalKey,
    string? ParentExternalId,
    string? Label,
    string? ScenarioTitle);

public record IngestReleaseInput([Required] string Kind, [Required] string ExternalId, string? State, string? Note);

public record IngestEndInput(string? Status, string? Reason);
