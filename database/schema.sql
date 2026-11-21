CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;

CREATE TABLE "Environments" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Environments" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "BaseUrl" TEXT NOT NULL,
    "IsDefault" INTEGER NOT NULL,
    "ProtectedIds" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL
);

CREATE TABLE "KnownBugs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_KnownBugs" PRIMARY KEY,
    "Title" TEXT NOT NULL,
    "Severity" TEXT NULL,
    "LastSeenAt" TEXT NOT NULL
);

CREATE TABLE "Notifications" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Notifications" PRIMARY KEY AUTOINCREMENT,
    "RunId" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "Payload" TEXT NOT NULL,
    "Target" TEXT NULL,
    "Delivered" INTEGER NOT NULL,
    "DeliveryError" TEXT NULL
);

CREATE TABLE "Runs" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Runs" PRIMARY KEY AUTOINCREMENT,
    "EnvironmentId" INTEGER NULL,
    "EnvironmentName" TEXT NOT NULL,
    "EnvironmentBaseUrl" TEXT NOT NULL,
    "Trigger" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "Scope" TEXT NOT NULL,
    "StartedBy" TEXT NULL,
    "ScheduleId" INTEGER NULL,
    "SuiteVersion" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "StartedAt" TEXT NULL,
    "FinishedAt" TEXT NULL,
    "DurationMs" INTEGER NULL,
    "Total" INTEGER NOT NULL,
    "Passed" INTEGER NOT NULL,
    "Failed" INTEGER NOT NULL,
    "ExpectedFailures" INTEGER NOT NULL,
    "UnexpectedPasses" INTEGER NOT NULL,
    "Skipped" INTEGER NOT NULL,
    "StatusReason" TEXT NULL
);

CREATE TABLE "Settings" (
    "Key" TEXT NOT NULL CONSTRAINT "PK_Settings" PRIMARY KEY,
    "Value" TEXT NULL
);

CREATE TABLE "TestCases" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_TestCases" PRIMARY KEY AUTOINCREMENT,
    "Key" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "File" TEXT NOT NULL,
    "Project" TEXT NOT NULL,
    "Tags" TEXT NOT NULL,
    "FirstSeenAt" TEXT NOT NULL
);

CREATE TABLE "Schedules" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Schedules" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Cron" TEXT NOT NULL,
    "TimeZone" TEXT NOT NULL,
    "EnvironmentId" INTEGER NOT NULL,
    "Scope" TEXT NOT NULL,
    "Enabled" INTEGER NOT NULL,
    "NextRunAt" TEXT NULL,
    "LastRunAt" TEXT NULL,
    "LastRunId" INTEGER NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Schedules_Environments_EnvironmentId" FOREIGN KEY ("EnvironmentId") REFERENCES "Environments" ("Id") ON DELETE CASCADE
);

CREATE TABLE "TestData" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_TestData" PRIMARY KEY AUTOINCREMENT,
    "RunId" INTEGER NOT NULL,
    "EnvironmentId" INTEGER NULL,
    "EnvironmentBaseUrl" TEXT NOT NULL,
    "ScenarioTitle" TEXT NULL,
    "Kind" TEXT NOT NULL,
    "ExternalId" TEXT NOT NULL,
    "NaturalKey" TEXT NULL,
    "ParentExternalId" TEXT NULL,
    "Label" TEXT NOT NULL,
    "State" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "ResolvedAt" TEXT NULL,
    "Note" TEXT NULL,
    CONSTRAINT "FK_TestData_Runs_RunId" FOREIGN KEY ("RunId") REFERENCES "Runs" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "ScenarioResults" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ScenarioResults" PRIMARY KEY AUTOINCREMENT,
    "RunId" INTEGER NOT NULL,
    "TestCaseId" INTEGER NOT NULL,
    "Ordinal" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "StartedAt" TEXT NULL,
    "FinishedAt" TEXT NULL,
    "DurationMs" INTEGER NULL,
    "ErrorMessage" TEXT NULL,
    "ErrorStack" TEXT NULL,
    "KnownBugs" TEXT NULL,
    CONSTRAINT "FK_ScenarioResults_Runs_RunId" FOREIGN KEY ("RunId") REFERENCES "Runs" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ScenarioResults_TestCases_TestCaseId" FOREIGN KEY ("TestCaseId") REFERENCES "TestCases" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Evidence" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Evidence" PRIMARY KEY AUTOINCREMENT,
    "ScenarioResultId" INTEGER NOT NULL,
    "Kind" TEXT NOT NULL,
    "FileName" TEXT NOT NULL,
    "ContentType" TEXT NOT NULL,
    "RelativePath" TEXT NOT NULL,
    "SizeBytes" INTEGER NOT NULL,
    "Sha256" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Evidence_ScenarioResults_ScenarioResultId" FOREIGN KEY ("ScenarioResultId") REFERENCES "ScenarioResults" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_Environments_Name" ON "Environments" ("Name");

CREATE INDEX "IX_Evidence_ScenarioResultId" ON "Evidence" ("ScenarioResultId");

CREATE INDEX "IX_Runs_EnvironmentId_CreatedAt" ON "Runs" ("EnvironmentId", "CreatedAt");

CREATE INDEX "IX_Runs_Status" ON "Runs" ("Status");

CREATE UNIQUE INDEX "IX_ScenarioResults_RunId_TestCaseId" ON "ScenarioResults" ("RunId", "TestCaseId");

CREATE INDEX "IX_ScenarioResults_TestCaseId_FinishedAt" ON "ScenarioResults" ("TestCaseId", "FinishedAt");

CREATE INDEX "IX_Schedules_EnvironmentId" ON "Schedules" ("EnvironmentId");

CREATE UNIQUE INDEX "IX_TestCases_Key" ON "TestCases" ("Key");

CREATE INDEX "IX_TestData_EnvironmentBaseUrl_State" ON "TestData" ("EnvironmentBaseUrl", "State");

CREATE INDEX "IX_TestData_Kind_ExternalId_EnvironmentBaseUrl" ON "TestData" ("Kind", "ExternalId", "EnvironmentBaseUrl");

CREATE INDEX "IX_TestData_RunId" ON "TestData" ("RunId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261121112222_InitialSchema', '8.0.11');

COMMIT;

BEGIN TRANSACTION;

CREATE TRIGGER trg_runs_frozen_after_finish BEFORE UPDATE ON Runs
WHEN OLD.FinishedAt IS NOT NULL
BEGIN SELECT RAISE(ABORT, 'Append-only: a finished run cannot be changed'); END;

CREATE TRIGGER trg_runs_no_delete BEFORE DELETE ON Runs
BEGIN SELECT RAISE(ABORT, 'Append-only: runs cannot be deleted'); END;

CREATE TRIGGER trg_results_frozen_after_finish BEFORE UPDATE ON ScenarioResults
WHEN OLD.FinishedAt IS NOT NULL
BEGIN SELECT RAISE(ABORT, 'Append-only: a finished scenario result cannot be changed'); END;

CREATE TRIGGER trg_results_no_delete BEFORE DELETE ON ScenarioResults
BEGIN SELECT RAISE(ABORT, 'Append-only: scenario results cannot be deleted'); END;

CREATE TRIGGER trg_evidence_no_update BEFORE UPDATE ON Evidence
BEGIN SELECT RAISE(ABORT, 'Evidence is immutable'); END;

CREATE TRIGGER trg_evidence_no_delete BEFORE DELETE ON Evidence
BEGIN SELECT RAISE(ABORT, 'Evidence is immutable'); END;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261121112233_AppendOnlyGuards', '8.0.11');

COMMIT;

