# User manual — Test Management Platform (Part 5)

How to start the platform, run the tests from it, watch them live, read the dashboard, and handle the everyday
tasks. The technical design is in `README.md`.

---

## 1. Starting the platform

The automation suite must be in the **same parent folder** as the platform, named either `SuperCom` or
`offenderwatch-qa-suite` (what `git clone` creates). The platform finds it automatically; `SUITE_DIR` overrides.

**Option A — without Docker** (needs .NET 8 SDK and Node 20):
```powershell
cd C:\Users\Neil\Desktop\SuperCom-Platform
npm start
```

**Option B — with Docker** (needs Docker Desktop running, about 15 GB free disk for the build):
```powershell
cd C:\Users\Neil\Desktop\SuperCom-Platform
npm run docker
```

Wait for **"Now listening on: http://…:5080"**, then open **http://localhost:5080**.
To stop it, press **Ctrl + C** in the terminal (`docker compose down` for Docker).

### Signing in

| User | Password | Can do |
|---|---|---|
| `operator` | `operator123` | everything |
| `viewer` | `viewer123` | look only (buttons are greyed out, with a tooltip) |

The green **● Live** dot (bottom-left) means the real-time connection is working. **Reconnecting…** means it's
catching up after a network blip; nothing is lost.

---

## 2. The screens

| Screen | What it's for |
|---|---|
| **Dashboard** | The answer to "can we release?": GO / NO-GO, each environment's latest complete run, the pass-rate trend, and what's failing (and since when). |
| **Runs** | Every run ever made. Start a run here, or tick two runs and compare them. |
| **Run page** | One run: watch it live, stop it, open each test's evidence. |
| **Test history** | One test across all runs: when it started failing, when it last passed, whether it's flaky. |
| **Compare runs** | What changed between two runs: regressed, recovered, new, missing. |
| **Test data** | The records the tests created in the application, and **Clean test data**. |
| **Environments** | The copies of the application you can test against. |
| **Schedules** | Automatic runs, e.g. every night at 02:00. |
| **Notifications** | Where regression alerts go (Slack / Teams webhook), and their log. |
| **API (Swagger)** | The platform's REST API, for developers. |

---

## 3. Everyday tasks

### Run the tests
1. Click **▶ Start run** (Dashboard or Runs).
2. Choose the **environment**. The default is pre-selected.
3. Choose **what to run**: Everything (71 tests, ~1 min), API only (~15 s) or UI only (~45 s).
4. Optional: a **filter**, one word such as `TC-21` or `@FR-04`, to run only matching tests.
5. Optional: **Leave test data for clean-up**, to practise the clean-up (the tests skip their own teardown).
6. Click **▶ Start run**. The run page opens.

### Watch a run live
- Each test moves **Queued → Running → result** by itself; there's no need to refresh.
- The bar and the counter ("37 of 71 finished") grow as tests finish.
- **Runner output** (at the bottom) shows the raw console of the test runner, streamed live.

### Stop a run
On the run page, click **■ Stop run**. Tests that hadn't finished are marked **Stopped**.

### Look at the evidence of a test
Click any test row. A side panel opens with tabs:
- **Summary**: the failure message and stack trace;
- **Screenshot** (UI tests): the screen at the end of the test or at the moment it failed;
- **Log**: every step the test performed, with timestamps;
- **API request / response** (API tests): the test's last call, i.e. the one that failed; "Show all calls"
  shows every call;
- **Error context**, and **All files**, with downloads and integrity hashes.

This works the same for old runs: open any past run, or **Open** in a test's history.

### Find out when a test started failing
**Test history** → search for the test (e.g. `TC-39`) → click it.
- The coloured boxes show **one box per run**, oldest → newest. **↓** marks a **regression** (it passed
  before and fails now); **↑** marks a **recovery**.
- The summary card answers: *failing since*, *last passed*, *flaky?*

### Compare two runs
**Runs** → tick two runs → **⇄ Compare**. Regressions and recoveries are listed first, then the change in totals.

### Clean up test data
**Test data** → choose the environment → **🧹 Clean test data** → **Clean now**.
- Only records created by test runs are deleted, through the application's own API.
- The 11 original offenders (protected ids, `1-11`) are **never** touched.
- Every record is re-checked before deletion; anything that doesn't match is left alone and reported.
- Location points can't be deleted on their own. After their offender is deleted the platform checks the
  application: still there = **"Left in the app — BUG-02 (verified)"**, proven gone = **"Removed with its
  offender (verified)"**, and if the reply proves nothing = **"Unknown — could not verify"**.
- The checkbox **"Only data still (or possibly) in the application"** shows everything that may still be
  there, including points left by BUG-02 and unverified records.

### Schedule a nightly run
**Schedules → + Add schedule** → pick **Every night at 02:00** (or type a cron expression) → environment →
**Save**. Scheduled runs show **Scheduled** as their trigger. Schedules survive a restart.

### Get notified about regressions
**Notifications** → paste a Slack or Teams incoming-webhook URL → **Save**. You get a message when a test that
passed the previous time it ran on that environment now **fails unexpectedly**. Known bugs never notify.
Without a webhook, alerts are only listed on the page.

### Add an environment
**Environments → + Add environment** → name, base URL (e.g. `https://…/AQApplication/YourName/`), protected ids
(`1-11`) → **Save**. Tick **Default** to make it the one the verdict and the Start-run dialog use.

---

## 4. Reading the dashboard

### GO / NO-GO
- It's decided from **the latest result of every test** on the default environment, whichever run produced it.
- A newer run (even an API-only or one-test run) updates the tests it ran; tests it didn't run keep their last
  result. So a 1-test run can't make the release GO by itself.
- **NO-GO** when a documented **Critical or High** bug still fails, a test fails **unexpectedly**, or a test of the
  suite has never run there.
- Below the banner: which runs the verdict was built from, and how it compares with the last complete run
  ("2 now pass, 0 now fail").

### Status colours (everywhere in the platform)

| Label | Colour | Meaning |
|---|---|---|
| ✓ Passed | green | the requirement works |
| ✕ Known bug | orange | fails because of a documented bug (with that bug's tag in the error); expected |
| ! Failed | red | **unexpected** failure: a new problem, needs attention. Also used when a known-bug test fails for a *different* reason. |
| ↻ Bug fixed? | blue | was expected to fail but passed; retest the bug |
| ■ Stopped / – Skipped | grey | didn't run to the end |

### Pass-rate trend
- **Filled points** are complete runs (all 71 tests).
- **Hollow points** are partial runs: API only, UI only, a filter, or incomplete. Their pass rate covers only
  part of the suite.
- Switch **All runs / Complete runs only** above the chart. **Show as table** lists every run with its kind.

### The 📎 number next to a test
The number of evidence files saved for it (log, screenshot, API calls…). Hover to see which, click the row to
open them.

---

## 5. Running the tests outside the platform

The suite still works on its own (see `SuperCom/MANUAL.md`). To report a command-line or CI run into the platform:

```powershell
cd C:\Users\Neil\Desktop\SuperCom
# in .env: PLATFORM_URL=http://localhost:5080  and  PLATFORM_TOKEN=<Platform:IngestToken from appsettings.json>
npm run platform                     # or: npm run platform -- -g TC-21
```

The run appears in the platform with the trigger **Cli**.

---

## 6. Common problems

| Problem | Fix |
|---|---|
| http://localhost:5080 doesn't open | The platform isn't running: start it (section 1). |
| "Run N is still active on …" | Only one run per environment at a time; wait for it or stop it. |
| "Suite folder not found" | Put the suite at `Desktop\SuperCom`, or set `Suite:WorkingDirectory` in `server/src/TestPlatform.Api/appsettings.json`. |
| Buttons are greyed out | You're signed in as `viewer`; sign out and in as `operator`. |
| Indicator says **Offline** | The platform stopped: restart it; the page reconnects by itself. |
| Docker build fails with "no space left on device" | Free at least 5 GB. Docker Desktop → Troubleshoot → **Clean / Purge data** removes old images. |
| A run shows **Error: "The platform restarted while this run was in progress"** | The platform was stopped mid-run; start a new run. |
