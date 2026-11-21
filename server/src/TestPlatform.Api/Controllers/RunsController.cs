using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestPlatform.Api.Contracts;
using TestPlatform.Api.Data;
using TestPlatform.Api.Domain;
using TestPlatform.Api.Infrastructure;
using TestPlatform.Api.Services;

namespace TestPlatform.Api.Controllers;

/// <summary>TM-02 — runs list, run detail, start, stop; B-02 — comparison.</summary>
[ApiController]
[Route("api/runs")]
public class RunsController(PlatformDbContext db, RunService runs, RunOrchestrator orchestrator, RunLauncher launcher,
    ComparisonService comparison, PlatformPaths paths) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<RunSummaryDto>> List([FromQuery] int? environmentId, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        var query = db.Runs.AsNoTracking();
        if (environmentId is not null) query = query.Where(r => r.EnvironmentId == environmentId);
        return (await query.OrderByDescending(r => r.Id).Take(Math.Clamp(take, 1, 500)).ToListAsync(ct)).Select(r => r.ToSummary()).ToList();
    }

    [HttpGet("{id:int}")]
    public Task<RunDetailDto> Get(int id, CancellationToken ct) => runs.GetDetailAsync(id, ct);

    /// <summary>Runner console output: live buffer while running, the saved log afterwards.</summary>
    [HttpGet("{id:int}/output")]
    public async Task<IReadOnlyList<string>> Output(int id, CancellationToken ct)
    {
        if (launcher.IsRunning(id)) return launcher.CurrentOutput(id);
        var file = paths.RunnerOutputFile(id);
        return System.IO.File.Exists(file) ? await System.IO.File.ReadAllLinesAsync(file, ct) : Array.Empty<string>();
    }

    [HttpPost]
    [Authorize(Policy = AuthSetup.OperatorPolicy)]
    public async Task<ActionResult<RunSummaryDto>> Start(StartRunInput input, CancellationToken ct)
    {
        var run = await orchestrator.StartAsync(
            new StartRunRequest(input.EnvironmentId, input.Scope, input.Grep, RunTrigger.Manual, User.Identity?.Name, KeepTestData: input.KeepTestData), ct);
        return CreatedAtAction(nameof(Get), new { id = run.Id }, run.ToSummary());
    }

    [HttpPost("{id:int}/stop")]
    [Authorize(Policy = AuthSetup.OperatorPolicy)]
    public async Task<IActionResult> Stop(int id, CancellationToken ct)
    {
        await orchestrator.StopAsync(id, User.Identity?.Name, ct);
        return Accepted();
    }

    [HttpGet("compare")]
    public Task<RunComparisonDto> Compare([FromQuery(Name = "base")] int baseRunId, [FromQuery(Name = "target")] int targetRunId, CancellationToken ct) =>
        comparison.CompareAsync(baseRunId, targetRunId, ct);
}
