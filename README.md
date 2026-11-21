# Test Management Platform — Part 5

> **New here?** Start with [`MANUAL.md`](MANUAL.md): start the platform, run tests, watch them live, read the dashboard, step by step.

A small full-stack application that **runs** the OffenderWatch automation suite (Part 3), **streams** its
progress live, and keeps the **full history** of every test with its evidence.

| | |
|---|---|
| Back end | ASP.NET Core 8 Web API (C#), Swagger at `/swagger` |
| Front end | React 18 + Vite (TypeScript), no UI framework, hand-drawn SVG charts |
| Database | SQLite through EF Core 8, with migrations |
| Real time | SignalR (`/hubs/runs`) |
| Suite | `../SuperCom`: Playwright, 71 scenarios (20 UI + 51 API) |

---

## 1. Run it on a clean machine

Get both repositories side by side, in one parent folder. The folder names don't matter: the platform finds the
suite as `SuperCom` or `offenderwatch-qa-suite`, or wherever `SUITE_DIR` points.

```bash
git clone https://github.com/NeilZer/offenderwatch-qa-suite.git
git clone https://github.com/NeilZer/offenderwatch-test-platform.git
cd offenderwatch-test-platform
```

Then pick one of two ways. Both are one command.

**A — without Docker** (needs .NET SDK 8+ and Node 20+): `npm start`

**B — Docker** (needs Docker Desktop and about 15 GB of free disk for the build): `npm run docker`
(or `SUITE_DIR=<path to the suite> docker compose up --build`)

`npm start`:
- checks the prerequisites;
- installs the suite and client dependencies if missing;
- builds the React client into the API;
- starts **one process**.

Either way, open **http://localhost:5080**: one port serves the UI, REST API, SignalR and Swagger. The SQLite
database is created from the EF Core migrations on first start.

| User | Password | Can do |
|---|---|---|
| `operator` | `operator123` | everything: start/stop runs, edit environments and schedules, clean test data |
| `viewer` | `viewer123` | watch runs, history, dashboard, evidence (action buttons disabled, API answers 403) |

**First steps on an empty database:**
1. **Environments → + Add environment.** For example "Neil (personal)" = `…/AQApplication/Neil/`, and
   "Shared (base)" = `…/AQApplication/`.
2. **▶ Start run.**

| Command (in this folder) | What it does |
|---|---|
| `npm start` | one-command start (B-05) |
| `npm run dev:api` + `npm run dev:client` | development: API on :5080, Vite with hot reload on :5173 (proxies `/api`, `/hubs`, `/swagger`) |
| `npm test` | 71 platform self-tests (B-06) |

**Configuration** lives in `server/src/TestPlatform.Api/appsettings.json`. No URL is hard-coded in the code (TM-01):

| Key | Meaning |
|---|---|
| `Platform:PublicUrl` | where the suite's reporter reaches the API (passed to the child process) |
| `Platform:DataDirectory` | folder for `platform.db`, the evidence files and the session keys (default: `SuperCom-Platform/data`) |
| `Platform:IngestToken` | shared secret the runner sends as `X-Platform-Token` |
| `Suite:WorkingDirectory` | path to the Part 3 suite |
| `Suite:Scopes` | scope name → Playwright arguments (`all`, `api`, `ui`) |
| `Auth:Users` | users with PBKDF2 password hashes (`dotnet run -- hash-password <pw>` prints a new hash) |

Target environments themselves (name + base URL) are managed **in the UI**, not in configuration.

---

## 2. How the automation reports into the platform: the **push** model

```
React UI ──Start run──► API ──launches──► npx playwright test   (child process, BASE_URL = chosen environment)
                         ▲                     │
                         │  HTTP, while it runs │  src/reporting/platform-reporter.ts  (Playwright reporter)
                         └──── /api/ingest/... ◄┘  src/helpers/test-data-tracker.ts    (test data, from the workers)
API ──SignalR──► every open browser (queued → running → passed/failed)
```

The platform **starts and stops** the process: that's needed for "start from the UI" and "stop mid-run".
**Results are pushed** by a Playwright reporter while the suite runs. Nothing is parsed from console output.

**Why push, not orchestrated parsing:**
1. **The results are exact.** The reporter sees Playwright's own objects: `expectedStatus` (xfail),
   annotations (`known-bug`), attachments (screenshots, traces) and steps. Parsing console text would
   reconstruct these from formatting, which breaks whenever the output changes.
2. **It's live by construction.** Each scenario is posted the moment it starts and ends (TM-03). It's
   not a replay after the suite finished.
3. **The contract is generic (5.7).** `/api/ingest/...` is plain HTTP + JSON. Any runner can use it. A run
   started **outside** the platform (command line, CI) registers itself: in the suite,
   `npm run platform [-- playwright args]` creates the run and then reports into it (trigger **Cli**). Any
   filter is recorded, so such a run is marked *partial* and never decides the verdict.

| Call | When |
|---|---|
| `POST /api/ingest/runs` | create a run (only for runs started outside the platform) |
| `POST /api/ingest/runs/{run}/begin` | all scenarios announced → they show as **Queued** |
| `POST …/scenarios/{result}/start` | scenario is **Running** |
| `POST …/scenarios/{result}/evidence` | log, screenshot, API request/response, error context, trace (multipart) |
| `POST …/scenarios/{result}/finish` | final status, duration, error message + stack, known bugs |
| `POST …/test-data`, `…/test-data/release` | data a scenario created / deleted (TM-06) |
| `POST …/end` | run finished |

**Expected failures keep their meaning.** A test marked with `knownBug('BUG-xx')` becomes **ExpectedFailure**
("Known bug", orange) **only if it failed with its own `[BUG-xx]` tag in the error**. If it failed for any other
reason (a timeout, a broken selector, a setup error), the reporter records it as **Failed** (red), with the
message "Expected to fail on BUG-xx, but it failed for a different reason", so `test.fail()` can never hide real
breakage. This is the same rule the Part 4 dashboard applies. A real problem is **Failed** (red). A known-bug test that suddenly
passes is **UnexpectedPass** ("Bug fixed?", blue). The three are kept apart in the run view, the history view
and the totals.

**If the platform goes down during a run:**
- After 3 consecutive network failures the reporter switches itself off for the rest of the run (circuit
  breaker), with one clear warning.
- The tests carry on at normal speed instead of waiting 15 s per call.

---

## 3. Database schema and why it is modelled this way

```
Environments ─┐ (id kept on Run as plain data + name/URL snapshot)
              ▼
Runs 1───* ScenarioResults *───1 TestCases
               │1
               └──* Evidence        (file on disk; path + size + SHA-256 here)
Runs 1───* TestData                  (records created in the target app, TM-06)
KnownBugs   Schedules   Notifications   Settings
```

| Table | Why |
|---|---|
| **TestCases** | A test's identity across runs is a stable **key**: `project › file › describe › title`. Its history is simply all its results. |
| **Runs** | Environment, trigger, scope/filter, start/end/duration, totals, and the **suite version** (hash of the suite's code). The environment's **name and URL are copied onto the run**, so the run stays true after the environment is edited or deleted. That's also why `EnvironmentId` is deliberately not a foreign key. |
| **ScenarioResults** | One row per test per run. Queued → Running → final status. Totals are always **recomputed from these rows**, never incremented. |
| **Evidence** | Metadata only; the bytes are files (see below). |
| **TestData** | Kind (offender / location point), id in the app, national ID (to verify before deleting), creating run and scenario, lifecycle state. |
| **KnownBugs** | Bug id → title + severity (sent by the runner from `qa/bugs.json`). Used for the go/no-go verdict. |

**Append-only (TM-05) is enforced by the database, not only by the code.** Migration `AppendOnlyGuards` adds
SQLite triggers:
- a **finished** run or result cannot be updated;
- runs, results and evidence can **never** be deleted;
- evidence can never be updated.

A new run only *inserts*. Try `UPDATE ScenarioResults SET Status='Passed'` and SQLite answers
*"Append-only: a finished scenario result cannot be changed"*. This is covered by `AppendOnlyTests`.

Schema from scratch: the EF Core migrations run automatically at start-up. The equivalent SQL script is
`database/schema.sql`.

### Evidence storage: files on disk, metadata in SQLite (TM-08)

`data/evidence/run-00015/result-01234/03-test-failed-1.png`, with the path, size and **SHA-256** stored in SQLite.

- **Byte-exact in git:** `.gitattributes` marks `data/**` as `-text`, so git never changes line endings; every
  hash also verifies in a fresh clone on Windows, macOS or Linux.
- **Why not blobs:** screenshots and traces are MBs. Keeping them out of SQLite keeps the database small,
  fast to back up and easy to inspect. The files can be opened directly.
- **Immutable anyway:**
  - every file is written **once** (`FileMode.CreateNew`) and marked read-only;
  - a failed write (e.g. disk full) deletes its partial file, so nothing half-written stays behind;
  - there's no API to change or delete evidence, and DB triggers block it;
  - evidence can only be attached while its scenario is still open;
  - `GET /api/evidence/{id}/verify` re-hashes the file and proves it's unchanged.
- **Safe to view:** evidence is served from the platform's own origin, so only inert content types are kept
  (images, text, JSON, zip). Anything else, e.g. an upload declared as `text/html`, is served as a download
  with `nosniff`. It can never run as a page.
- **What is captured, per scenario:**
  - **every scenario:** the execution log (steps with timestamps), a full-page screenshot (UI), and the API
    request/response pairs (API);
  - **failed scenarios, in addition:** message + stack trace, the screenshot at failure, Playwright's error
    context, and the trace for unexpected failures.
- **The offending request/response is the scenario's own last call.** Calls made during clean-up are tagged
  `teardown` and never shown as the offending one.

---

## 4. Requirements: where to see each one

| ID | What was built | How to see it |
|---|---|---|
| **TM-01** | Environments CRUD + default; the run stores the environment it used; no hard-coded URLs | **Environments** page; the **Start run** dialog preselects the default |
| **TM-02** | Start / watch / stop from the UI; runs list with environment, start, end, duration, trigger, totals; per-scenario duration + failure message | **Runs** → **▶ Start run** → the run page → **■ Stop run** |
| **TM-03** | SignalR: each scenario goes Queued → Running → result live, plus the live runner console | Open a running run; nothing needs a refresh |
| **TM-04** | Per-test history, oldest → newest; Regression ↓ / Recovery ↑; failing since; last passed; flaky | **Test history** → e.g. *TC-39 last seen…* |
| **TM-05** | SQLite + migrations, append-only triggers | `database/schema.sql`, `AppendOnlyTests` |
| **TM-06** | Data registered the moment it's created, attributed to its run; **Clean test data**; seeded ids protected | **Test data** page → **🧹 Clean test data** |
| **TM-07** | Landing page: go / no-go, latest run per environment, pass-rate trend, currently failing (and for how long) | **Dashboard** |
| **TM-08** | Log + screenshot / request-response per scenario; more for failures; immutable; viewable from the run and history views | Click any scenario, or **Open** in a history row |

**Real-time behaviour on stop, disconnect and reconnect:**
- **Stop** kills the whole process tree (`taskkill /T /F` on Windows: .NET's tree-kill left the Playwright
  runner alive, which I found in testing). Unfinished scenarios become **Stopped**.
- **The browser loses its connection:** the indicator shows *Reconnecting…*. On reconnect the client
  re-joins its groups and **reloads the run from the API**, so nothing that happened while offline is lost.
  This was tested by cutting the SignalR connection for 10 s mid-run: the page caught up from 1 to 51
  scenarios, with **no page reload**.
- **The platform restarts during a run:** at start-up, open runs are closed as *Error: "The platform
  restarted while this run was in progress"*, instead of staying "Running" forever.
- **Two starts at once:** "one active run per environment" is checked and claimed under a lock, so a
  double-click or the scheduler firing together with a user can't start two runs on one environment.
  Invalid input (unknown scope, unsafe filter) is rejected with **400 before any run is created**.

**Go / no-go (TM-07)** is decided from **the latest result of every test** on the default environment, whichever
run produced it (`Domain/ReleaseVerdict.cs`, unit-tested):
- A newer run (even an API-only or one-test run) updates the tests it executed. Tests it didn't run keep their
  last result, so "1 of 1 passed" can never make a release GO by itself.
- **NO-GO** if any test fails unexpectedly, any documented **Critical/High** bug still fails, or a test of the
  suite has never run there.
- The banner says which runs the verdict was built from, and **how it compares with the last complete run**
  (how many tests now pass or now fail), with a link to the run comparison.

**Dashboard details:**
- Each environment card shows its **latest complete run**, so a 1-test run never shows as "100%". Newer partial
  runs are listed under it.
- The pass-rate trend shows **every completed run**: complete runs as filled points, partial runs as hollow
  points, with a switch to show complete runs only.

### Test data lifecycle (TM-06)

1. `TestDataTracker` (in the suite) registers every offender **right after creating it**, with its national ID,
   the run and the scenario.
2. After a normal teardown the record becomes *Deleted by its test*.
3. If a run is **stopped mid-test** (no teardown), the record stays *Still in the application*.
4. **Clean test data** deletes it through the application's own `DELETE /api/offenders/{id}`. Before each
   delete, the platform re-reads the record and checks that the **national ID still matches**, so an id the
   app re-used for someone else is left alone.
5. A record whose delete **failed** (e.g. the application was unreachable) is **retried** by the next clean-up.
6. **Seeded ids (`1-11`, configurable per environment) are never deleted.** If a scenario even tries to register
   one, it's stored as *Protected*.
7. "Leave test data for clean-up" in the Start-run dialog skips the scenarios' own teardown, to demonstrate the
   clean-up.

**Location points are checked, never assumed.** The application has no endpoint to delete a single point.
Points are only looked at when their offender was **really deleted** (by its test, by the clean-up, or already
gone); the points of a protected or not-deleted offender are never touched. Then the platform asks the
application for the trail, and accepts only replies that prove something:
- points still returned are marked **"Left in the application (BUG-02)"**: the app doesn't cascade the delete;
- points **proven** gone (HTTP 404, or an empty JSON list) are marked **"Removed with its offender (verified)"**;
- any reply that proves nothing (empty body, JSON that isn't a list, an entry without an id, an error status, a
  network failure) gives **"Unknown — could not verify"**, never a verified state;
- if the offender's id now belongs to someone else, the record is **"Id now belongs to another record"**, and its
  points are *Unknown* (the other person's trail says nothing about ours).

The **"Only data still (or possibly) in the application"** filter shows everything that may still be there:
waiting for clean-up, failed clean-ups, points left by BUG-02, and unverified records.

The 286 older records that had been labelled "removed" by assumption were all checked against the live
application on 21/11/2026, found still present, and relabelled by migration `VerifiedOrphanedPoints`.

---

## 5. Bonus challenges

| # | Done | How to see it working |
|---|---|---|
| **B-01** Flakiness | ✅ | **Test history** shows a ⚠ Flaky badge. *Rule:* within the last **10** runs on the **same environment**, count the pass/fail changes between consecutive runs that used the **same suite version** (same code). **2+ changes** (pass → fail → pass) = flaky. One change is a real regression or recovery, and a change across code versions is explained by the code. Covered by the history/flakiness unit tests. |
| **B-02** Run comparison | ✅ | **Runs** → tick two runs → **⇄ Compare**. Shows regressed, recovered, status changed, new / missing, and the change in totals. Try runs #14 → #15, or #19 → #22. |
| **B-03** Regression notifications | ✅ | **Notifications**: set a Slack/Teams webhook. A test that **passed the previous time it ran on that environment** and now fails **unexpectedly** triggers it. Known-bug failures never do, and a partial run in between doesn't hide a regression. Every notification is logged, even without a webhook. See run #15. |
| **B-04** Scheduled runs | ✅ | **Schedules**: cron + time zone, presets (nightly 02:00…). Stored in SQLite, so it survives a restart (tested); its runs show trigger **Scheduled** (run #17). Occurrences missed while the API was down are skipped, not replayed in a burst. |
| **B-05** One-command startup | ✅ both verified | **Docker:** `npm run docker` (finds the suite folder) or `docker compose up --build`. Verified with Docker Desktop 29.9: a 3.7 GB image (Playwright browsers + ASP.NET Core 8 + the suite) on a fresh volume; a full suite run inside the container gave the same 26 passed / 45 known bug as on Windows, with screenshots stored and verified. The container keeps its database on its own named volume, so it never shares a SQLite file with a platform on the host. Session keys are stored on the data volume, so sign-ins survive a container restart. **Without Docker:** `npm start`. |
| **B-06** Platform self-tests | ✅ | `npm test`: **71 tests**: history/transitions, flakiness, the go/no-go rules, the clean-up guard end to end (real SQLite from the real migrations + a fake OffenderWatch API; only our own record gets a DELETE; failed clean-ups are retried), append-only triggers, notifier rules, evidence storage (read-only, hash, no partial file after a failed upload, content-type safety). |
| **B-07** Authentication | ✅ | Sign in as `viewer`: every action button is disabled with a tooltip, and the API answers **403**. The runner uses its own token on the ingestion endpoints only. |

---

## 6. The included database (deliverable 6)

`data/platform.db` + `data/evidence/` contain **28 real runs on 2 environments** (history is append-only, so
nothing was removed):

| Runs | Environment | What they show |
|---|---|---|
| #1–#7 | Neil (personal) | Made while building and checking the platform: API-only runs; **#4 stopped mid-run**; #5 reported no scenarios (a quoting bug in the filter, fixed; such a run is now recorded as *Error*); #6 an invalid filter (now rejected with HTTP 400 before any run is created); **#7** kept its test data to demonstrate *Clean test data*. |
| #8–#10, #14, #16 | Neil (personal) | Full suite: **26 passed / 45 known bug / 0 failed**, identical to the standalone suite |
| **#13, #15** | Neil (personal) | **Regressions**, produced honestly by adjusting a scenario (§5.4): TC-39 temporarily expected the wrong timezone (UTC) → **Failed**, with screenshot, log, error context and trace. Reverted for #14 and #16 → **Recovery**. #15 also triggered the B-03 notification. Each change has a different suite version, which is why B-01 correctly does **not** call TC-39 flaky. |
| #17 | Neil (personal) | **Scheduled** run (B-04) |
| #18 | Neil (personal) | UI-only run started from the dialog |
| **#19** | **Shared (base)** | First run on the shared instance, which other candidates also use. Two **unexpected failures**: TC-01 (my test compared a name with an empty value, created by someone else's BUG-10 data, slightly differently from the screen; fixed in the suite) and TC-35 (**the machine's disk ran out of space** during the run, so the following 16 scenarios were skipped and 10 evidence uploads failed; the half-written files were removed, and the store now cleans these up itself). |
| #20, #21 | Neil (personal) | Started from the **command line** (`npm run platform`, trigger **Cli**); #21 shows a filtered run recorded as partial |
| **#22** | **Shared (base)** | Full suite on the shared instance: **26 / 45 / 0**. TC-01 and TC-35 show a **Recovery** on this environment. The shared instance had the same number of offenders before and after: every record our tests created was removed. |
| #23, #24 | Neil (personal) | Verification of review fixes, both stopped on purpose. #23: two *Start run* requests sent at the same instant → one run created, the other rejected with 409 (start lock). #24: an HTML file uploaded as evidence (`evil.html`) is stored and served only as a download (`application/octet-stream`, `nosniff`), never as a page. |
| #25, #26 | Neil (personal) | Browser tests of the live view: the real-time connection was cut for 10 s mid-run; the page showed *Reconnecting…* and then caught up to 51/51 with no page reload. |
| #27 | Neil (personal) | Full suite under the stricter rule (a known bug counts as expected only with its own `[BUG-xx]` tag): still **26 / 45 / 0**, so every known-bug test really fails on its documented bug. |
| #28 | Neil (personal) | Check of that rule with two temporary tests (deleted afterwards): the one failing **without** its tag → **Failed**, the one failing **with** it → *Known bug*. |

---

## 7. Project layout

```
SuperCom-Platform/
├── package.json · scripts/start.mjs          one-command start (B-05)
├── Dockerfile · Dockerfile.dockerignore · docker-compose.yml   Docker one-command start (B-05)
├── database/schema.sql                       schema script (generated from the migrations)
├── data/                                     platform.db + evidence/ (deliverable 6)
├── server/
│   ├── src/TestPlatform.Api/
│   │   ├── Domain/          entities · HistoryAnalyzer (TM-04, B-01) · ReleaseVerdict (TM-07) · CleanupGuard (TM-06) — pure logic
│   │   ├── Data/            DbContext + Migrations (InitialSchema, AppendOnlyGuards, RunFilter)
│   │   ├── Contracts/       request/response DTOs (the API surface)
│   │   ├── Services/        RunService (ingest/finalise) · RunLauncher (process) · RunOrchestrator (start/stop rules)
│   │   │                    EvidenceStore · TestDataService · HistoryService · DashboardService · ComparisonService
│   │   │                    RegressionNotifier (B-03) · Scheduling (B-04) · Mapper
│   │   ├── Hubs/            RunsHub + RunEvents (SignalR)
│   │   ├── Controllers/     thin REST controllers + the ingestion contract
│   │   └── Infrastructure/  options/paths · auth (B-07) · problem-details error handling
│   └── tests/TestPlatform.Tests/   xUnit (B-06)
└── client/src/
    ├── api/          typed API client (one place for every endpoint, readable error messages)
    ├── realtime/     one SignalR connection, reconnect + resync
    ├── auth/         session + role
    ├── components/   status pills, totals bar, evidence panel, trend chart, dialogs
    ├── lib/          formatting, data-loading hook
    └── pages/        Dashboard · Runs · Run detail · Test history · Compare · Test data · Environments · Schedules · Notifications
```

**Error handling:** domain errors are typed (`NotFoundException`, `ConflictException`, `BadRequestException`) and
turned into RFC 7807 problem responses (404/409/400) in **one place**. Unexpected errors are logged and return a
generic 500, never a stack trace. The client shows the server's message in plain language.

## 8. Known limitations

- **Docker build after the last restructure:** the build now uses two named contexts (platform + suite), so
  folder names no longer matter. `docker compose config` was validated for both folder layouts, but the image
  was not rebuilt after this change: the development machine didn't have the ~15 GB of free disk the build
  needs. The previous layout was fully built and verified (see B-05).
- One active run per environment: the suite shares one live instance and asserts exact counter changes.
- The ingestion token and demo passwords are in `appsettings.json` for the assignment. In production they would
  come from a secret store, and sign-in would get rate limiting.
- One API process owns the SQLite database (the start lock is in-process). Scaling out to several API instances
  would need a server database and a distributed lock.
- Location points created by tests stay in the application after their offender is deleted (**BUG-02**). The
  platform records them as *removed with their offender* and does not hide the defect.
