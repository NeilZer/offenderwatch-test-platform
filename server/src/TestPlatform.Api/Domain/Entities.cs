namespace TestPlatform.Api.Domain;

/// <summary>A target the suite can run against (TM-01). Name + base URL, one may be the default.</summary>
public class TargetEnvironment
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public bool IsDefault { get; set; }
    /// <summary>Seeded record ids that "Clean test data" must never delete, e.g. "1-11" (TM-06).</summary>
    public string ProtectedIds { get; set; } = "1-11";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// One execution of the suite (TM-02). The environment is stored as a SNAPSHOT (name + URL) so the
/// run stays truthful even if the environment is later edited or deleted.
/// Mutable only while it is running; a database trigger blocks updates once FinishedAt is set.
/// </summary>
public class Run
{
    public int Id { get; set; }
    /// <summary>Informational id, deliberately NOT a foreign key: deleting an environment must never touch a frozen run.</summary>
    public int? EnvironmentId { get; set; }
    public string EnvironmentName { get; set; } = "";
    public string EnvironmentBaseUrl { get; set; } = "";
    public RunTrigger Trigger { get; set; }
    public RunStatus Status { get; set; }
    public string Scope { get; set; } = "all";
    /// <summary>Test filter (grep) when only part of the scope ran. Filtered runs never drive the verdict or the trend.</summary>
    public string? Filter { get; set; }
    public string? StartedBy { get; set; }
    public int? ScheduleId { get; set; }
    /// <summary>Fingerprint of the suite's source code — lets flakiness detection know "no code change".</summary>
    public string? SuiteVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public long? DurationMs { get; set; }
    public int Total { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int ExpectedFailures { get; set; }
    public int UnexpectedPasses { get; set; }
    public int Skipped { get; set; }
    public string? StatusReason { get; set; }
    public List<ScenarioResult> Results { get; set; } = new();

    /// <summary>A complete, unfiltered run of the whole suite — the only kind that may decide go / no-go.</summary>
    public bool IsFullSuite => Scope == "all" && string.IsNullOrEmpty(Filter);
}

public enum RunTrigger { Manual, Scheduled, Cli, Api }

public enum RunStatus { Queued, Running, Completed, Stopped, Error }

/// <summary>A test scenario, identified across runs by a stable key (project + file + title path).</summary>
public class TestCase
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string File { get; set; } = "";
    public string Project { get; set; } = "";
    /// <summary>Space-separated tags, e.g. "TC-03 API-01 FR-01".</summary>
    public string Tags { get; set; } = "";
    public DateTimeOffset FirstSeenAt { get; set; }
    public List<ScenarioResult> Results { get; set; } = new();
}

/// <summary>
/// The result of one test case in one run (append-only history, TM-05).
/// Lifecycle: Queued → Running → final status. Once FinishedAt is set the row is frozen by a DB trigger.
/// </summary>
public class ScenarioResult
{
    public int Id { get; set; }
    public int RunId { get; set; }
    public Run? Run { get; set; }
    public int TestCaseId { get; set; }
    public TestCase? TestCase { get; set; }
    public int Ordinal { get; set; }
    public ScenarioStatus Status { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public long? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorStack { get; set; }
    /// <summary>Space-separated documented bug ids the scenario is expected to fail on, e.g. "BUG-03".</summary>
    public string? KnownBugs { get; set; }
    public List<Evidence> Evidence { get; set; } = new();
}

/// <summary>
/// Passed / Failed are plain outcomes.
/// ExpectedFailure = failed on a documented bug (xfail) — NOT a surprise.
/// UnexpectedPass  = was expected to fail but passed — the bug looks fixed, retest it.
/// Interrupted     = the run was stopped while the scenario was queued or running.
/// </summary>
public enum ScenarioStatus { Queued, Running, Passed, Failed, ExpectedFailure, UnexpectedPass, Skipped, Interrupted }

/// <summary>Immutable evidence file attached to one scenario result (TM-08). Bytes on disk, metadata here.</summary>
public class Evidence
{
    public int Id { get; set; }
    public int ScenarioResultId { get; set; }
    public ScenarioResult? ScenarioResult { get; set; }
    public EvidenceKind Kind { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public enum EvidenceKind { Log, Screenshot, ApiExchange, ErrorContext, Trace, Other }

/// <summary>Documented defect a scenario can be expected to fail on. Upserted from runner metadata.</summary>
public class KnownBug
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Severity { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}

/// <summary>Data a scenario created in the target environment (TM-06), attributed to its run.</summary>
public class TestDataRecord
{
    public int Id { get; set; }
    public int RunId { get; set; }
    public Run? Run { get; set; }
    public int? EnvironmentId { get; set; }
    public string EnvironmentBaseUrl { get; set; } = "";
    public string? ScenarioTitle { get; set; }
    public TestDataKind Kind { get; set; }
    /// <summary>Id in the target application (e.g. offender id).</summary>
    public string ExternalId { get; set; } = "";
    /// <summary>Business identity used to verify the record before deleting it (offender: national ID).</summary>
    public string? NaturalKey { get; set; }
    public string? ParentExternalId { get; set; }
    public string Label { get; set; } = "";
    public TestDataState State { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? Note { get; set; }
}

public enum TestDataKind { Offender, LocationPoint }

public enum TestDataState { Active, DeletedByTest, CleanedByPlatform, AlreadyGone, Protected, CleanupFailed, RemovedWithParent }

/// <summary>Recurring run (B-04). Survives restarts because it lives in the database.</summary>
public class Schedule
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Cron { get; set; } = "";
    public string TimeZone { get; set; } = "UTC";
    public int EnvironmentId { get; set; }
    public TargetEnvironment? Environment { get; set; }
    public string Scope { get; set; } = "all";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset? NextRunAt { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public int? LastRunId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Outgoing regression notification (B-03) — kept as an audit trail even if delivery fails.</summary>
public class Notification
{
    public int Id { get; set; }
    public int RunId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Summary { get; set; } = "";
    public string Payload { get; set; } = "";
    public string? Target { get; set; }
    public bool Delivered { get; set; }
    public string? DeliveryError { get; set; }
}

/// <summary>Simple key/value settings editable in the UI (e.g. webhook URL).</summary>
public class Setting
{
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}
