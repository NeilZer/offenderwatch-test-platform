using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestPlatform.Api.Contracts;
using TestPlatform.Api.Infrastructure;
using TestPlatform.Api.Services;

namespace TestPlatform.Api.Controllers;

/// <summary>
/// The runner → platform contract (push model). Generic on purpose: any runner that can make HTTP
/// calls (Playwright, pytest, a shell script) can report into the platform.
///
///   POST /api/ingest/runs                                   create a run (only for runs started outside the platform)
///   POST /api/ingest/runs/{run}/begin                       announce all scenarios → they appear as "queued"
///   POST /api/ingest/runs/{run}/scenarios/{result}/start    scenario is running
///   POST /api/ingest/runs/{run}/scenarios/{result}/evidence multipart file + kind (log, screenshot, apiexchange, …)
///   POST /api/ingest/runs/{run}/scenarios/{result}/finish   final status + error + known bugs
///   POST /api/ingest/runs/{run}/test-data                   register data the scenario created (TM-06)
///   POST /api/ingest/runs/{run}/test-data/release           the scenario deleted that data itself
///   POST /api/ingest/runs/{run}/end                         run finished
/// Authentication: header X-Platform-Token.
/// </summary>
[ApiController]
[Route("api/ingest/runs")]
[Authorize(Policy = AuthSetup.RunnerPolicy)]
public class IngestController(RunService runs, TestDataService testData, RegressionNotifier notifier) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RunSummaryDto>> Create(IngestCreateRunInput input, CancellationToken ct) =>
        Ok((await runs.CreateRunForIngestAsync(input, ct)).ToSummary());

    [HttpPost("{runId:int}/begin")]
    public Task<IngestBeginResult> Begin(int runId, IngestBeginInput input, CancellationToken ct) => runs.BeginAsync(runId, input, ct);

    [HttpPost("{runId:int}/scenarios/{resultId:int}/start")]
    public async Task<IActionResult> Start(int runId, int resultId, CancellationToken ct)
    {
        await runs.StartScenarioAsync(runId, resultId, ct);
        return NoContent();
    }

    [HttpPost("{runId:int}/scenarios/{resultId:int}/evidence")]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<ActionResult<EvidenceDto>> Evidence(int runId, int resultId, [FromForm] string? kind, IFormFile file, CancellationToken ct) =>
        Ok((await runs.AddEvidenceAsync(runId, resultId, kind, file, ct)).ToDto());

    [HttpPost("{runId:int}/scenarios/{resultId:int}/finish")]
    public async Task<IActionResult> Finish(int runId, int resultId, IngestFinishInput input, CancellationToken ct)
    {
        await runs.FinishScenarioAsync(runId, resultId, input, ct);
        return NoContent();
    }

    [HttpPost("{runId:int}/test-data")]
    public async Task<ActionResult<TestDataDto>> RegisterTestData(int runId, IngestTestDataInput input, CancellationToken ct) =>
        Ok((await testData.RegisterAsync(runId, input, ct)).ToDto(null));

    [HttpPost("{runId:int}/test-data/release")]
    public async Task<IActionResult> ReleaseTestData(int runId, IngestReleaseInput input, CancellationToken ct)
    {
        await testData.ReleaseAsync(runId, input, ct);
        return NoContent();
    }

    [HttpPost("{runId:int}/end")]
    public async Task<IActionResult> End(int runId, IngestEndInput input, CancellationToken ct)
    {
        await runs.EndAsync(runId, input, ct);
        await notifier.OnRunFinishedAsync(runId, ct);
        return NoContent();
    }
}
