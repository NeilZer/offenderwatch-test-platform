/** Mirrors the server DTOs (TestPlatform.Api/Contracts/Dtos.cs). */

export type ScenarioStatus =
  | 'Queued' | 'Running' | 'Passed' | 'Failed' | 'ExpectedFailure' | 'UnexpectedPass' | 'Skipped' | 'Interrupted';

export type RunStatus = 'Queued' | 'Running' | 'Completed' | 'Stopped' | 'Error';

export interface User { username: string; role: string; canOperate: boolean }

export interface Environment {
  id: number; name: string; baseUrl: string; isDefault: boolean; protectedIds: string; createdAt: string; updatedAt: string;
}

export interface RunTotals { total: number; passed: number; failed: number; expectedFailures: number; unexpectedPasses: number; skipped: number }

export interface RunSummary {
  id: number; environmentId: number | null; environmentName: string; environmentBaseUrl: string;
  trigger: string; status: RunStatus; scope: string; filter: string | null; isFullSuite: boolean; startedBy: string | null;
  createdAt: string; startedAt: string | null; finishedAt: string | null; durationMs: number | null;
  totals: RunTotals; completed: number; statusReason: string | null;
}

export interface KnownBug { id: string; title: string; severity: string | null }

export interface Evidence { id: number; kind: string; fileName: string; contentType: string; sizeBytes: number; sha256: string; createdAt: string; url: string }

export interface Scenario {
  id: number; runId: number; testCaseId: number; title: string; file: string; project: string; tags: string; ordinal: number;
  status: ScenarioStatus; startedAt: string | null; finishedAt: string | null; durationMs: number | null;
  errorMessage: string | null; errorStack: string | null; knownBugs: KnownBug[]; evidence: Evidence[];
}

export interface RunDetail { run: RunSummary; scenarios: Scenario[] }

export interface EnvironmentSummary {
  environmentId: number | null; environmentName: string; current: 'Pass' | 'Fail' | 'NotRun'; currentStatus: ScenarioStatus | null;
  lastPassedAt: string | null; failingSince: string | null; failingSinceRunId: number | null; isFlaky: boolean; flips: number; executions: number;
}

export interface TestCaseItem { id: number; title: string; file: string; project: string; tags: string; executions: number; environments: EnvironmentSummary[] }

export interface HistoryEntry {
  resultId: number; runId: number; environmentId: number | null; environmentName: string; at: string; status: ScenarioStatus;
  verdict: 'Pass' | 'Fail' | 'NotRun'; transition: 'None' | 'FirstResult' | 'Regression' | 'Recovery';
  durationMs: number | null; errorMessage: string | null; knownBugs: KnownBug[]; suiteVersion: string | null; evidenceCount: number;
}

export interface TestHistory { testCase: TestCaseItem; entries: HistoryEntry[]; rule: { window: number; minFlips: number; description: string } }

export interface TrendPoint {
  runId: number; environmentId: number | null; environmentName: string; at: string; passRate: number; passed: number; executed: number;
  isFullSuite: boolean; scope: string; filter: string | null; skipped: number;
}

export interface FailingTest {
  testCaseId: number; title: string; tags: string; environmentName: string; status: ScenarioStatus; expected: boolean;
  failingSince: string | null; failingSinceRunId: number | null; lastPassedAt: string | null; knownBugs: KnownBug[]; isFlaky: boolean;
}

export interface Notification { id: number; runId: number; createdAt: string; summary: string; target: string | null; delivered: boolean; deliveryError: string | null }

export interface VerdictBasis {
  testsConsidered: number; testsWithResult: number; testsNeverRun: number; passing: number; unexpectedFailures: number;
  blockingBugIds: string[]; sourceRunIds: number[]; baselineRunId: number | null; improvedSinceBaseline: number; worsenedSinceBaseline: number;
}

export interface Dashboard {
  verdict: { go: boolean; line: string; environmentName: string | null; runId: number | null; basis: VerdictBasis | null };
  latestRunPerEnvironment: { run: RunSummary; isComplete: boolean; newerRunIds: number[] }[]; trend: TrendPoint[]; currentlyFailing: FailingTest[];
  activeRuns: RunSummary[]; recentNotifications: Notification[];
}

export interface ComparisonRow { testCaseId: number; title: string; project: string; baseStatus: ScenarioStatus | null; targetStatus: ScenarioStatus | null }

export interface Comparison {
  base: RunSummary; target: RunSummary; regressed: ComparisonRow[]; recovered: ComparisonRow[]; statusChanged: ComparisonRow[];
  new: ComparisonRow[]; missing: ComparisonRow[]; unchanged: number; delta: RunTotals;
}

export interface TestDataRecord {
  id: number; runId: number; environmentBaseUrl: string; environmentName: string | null; scenarioTitle: string | null; kind: string;
  externalId: string; naturalKey: string | null; parentExternalId: string | null; label: string; state: string;
  createdAt: string; resolvedAt: string | null; note: string | null;
}

export interface CleanupReport { examined: number; deleted: number; alreadyGone: number; protected: number; skipped: number; failed: number; details: string[] }

export interface Schedule {
  id: number; name: string; cron: string; timeZone: string; environmentId: number; environmentName: string | null; scope: string;
  enabled: boolean; nextRunAt: string | null; lastRunAt: string | null; lastRunId: number | null;
}
