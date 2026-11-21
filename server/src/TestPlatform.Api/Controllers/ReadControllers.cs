using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestPlatform.Api.Contracts;
using TestPlatform.Api.Data;
using TestPlatform.Api.Infrastructure;
using TestPlatform.Api.Services;

namespace TestPlatform.Api.Controllers;

/// <summary>TM-04 — test cases and their history.</summary>
[ApiController]
[Route("api/tests")]
public class TestsController(HistoryService history) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<TestCaseListItemDto>> List(CancellationToken ct) => history.ListAsync(ct);

    [HttpGet("{id:int}/history")]
    public Task<TestHistoryDto> History(int id, CancellationToken ct) => history.GetAsync(id, ct);
}

/// <summary>TM-08 — a single scenario result (with evidence), from any run, current or past.</summary>
[ApiController]
[Route("api/results")]
public class ResultsController(RunService runs) : ControllerBase
{
    [HttpGet("{id:int}")]
    public Task<ScenarioDto> Get(int id, CancellationToken ct) => runs.GetResultAsync(id, ct);
}

/// <summary>TM-07 — landing page data.</summary>
[ApiController]
[Route("api/dashboard")]
public class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public Task<DashboardDto> Get(CancellationToken ct) => dashboard.GetAsync(ct);
}

/// <summary>TM-08 — evidence files (read-only: there is no endpoint to change or delete evidence).</summary>
[ApiController]
[Route("api/evidence")]
public class EvidenceController(PlatformDbContext db, EvidenceStore store) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Download(int id, [FromQuery] bool download = false, CancellationToken ct = default)
    {
        var evidence = await db.Evidence.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException($"Evidence {id} not found");
        var path = store.FullPath(evidence);
        if (!System.IO.File.Exists(path)) throw new NotFoundException($"Evidence file for {id} is missing on disk");
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        Response.Headers.XContentTypeOptions = "nosniff"; // the browser must not guess a more dangerous type
        // Re-check at serve time too (rows stored before the allow-list existed), and download anything not inline-safe.
        var contentType = EvidenceStore.SafeContentType(evidence.ContentType);
        var asAttachment = download || !EvidenceStore.CanDisplayInline(contentType);
        return PhysicalFile(path, contentType, asAttachment ? evidence.FileName : null);
    }

    /// <summary>Re-hashes the file and compares it with the SHA-256 recorded at upload time.</summary>
    [HttpGet("{id:int}/verify")]
    public async Task<object> Verify(int id, CancellationToken ct)
    {
        var evidence = await db.Evidence.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException($"Evidence {id} not found");
        return new { evidence.Id, evidence.Sha256, intact = await store.VerifyAsync(evidence, ct) };
    }
}

/// <summary>TM-06 — accumulated test data and "Clean test data".</summary>
[ApiController]
[Route("api/test-data")]
public class TestDataController(TestDataService testData) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<TestDataDto>> List([FromQuery] int? environmentId, [FromQuery] bool activeOnly = false, CancellationToken ct = default) =>
        testData.ListAsync(environmentId, activeOnly, ct);

    [HttpPost("clean")]
    [Authorize(Policy = AuthSetup.OperatorPolicy)]
    public Task<CleanupReportDto> Clean(CleanupInput input, CancellationToken ct) => testData.CleanAsync(input.EnvironmentId, ct);
}

/// <summary>B-04 — schedules.</summary>
[ApiController]
[Route("api/schedules")]
public class SchedulesController(ScheduleService schedules) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ScheduleDto>> List(CancellationToken ct) => schedules.ListAsync(ct);

    [HttpPost]
    [Authorize(Policy = AuthSetup.OperatorPolicy)]
    public Task<ScheduleDto> Create(ScheduleInput input, CancellationToken ct) => schedules.CreateAsync(input, ct);

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthSetup.OperatorPolicy)]
    public Task<ScheduleDto> Update(int id, ScheduleInput input, CancellationToken ct) => schedules.UpdateAsync(id, input, ct);

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthSetup.OperatorPolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await schedules.DeleteAsync(id, ct);
        return NoContent();
    }
}

/// <summary>B-03 — webhook setting and the notification log.</summary>
[ApiController]
[Route("api/settings")]
public class SettingsController(PlatformDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<SettingsDto> Get(CancellationToken ct) =>
        new((await db.Settings.FindAsync([RegressionNotifier.WebhookSettingKey], ct))?.Value);

    [HttpPut]
    [Authorize(Policy = AuthSetup.OperatorPolicy)]
    public async Task<SettingsDto> Put(SettingsDto input, CancellationToken ct)
    {
        var url = input.WebhookUrl?.Trim();
        if (!string.IsNullOrEmpty(url) && (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw new BadRequestException("Webhook URL must be an absolute https URL");
        var setting = await db.Settings.FindAsync([RegressionNotifier.WebhookSettingKey], ct);
        if (setting is null) db.Settings.Add(setting = new Domain.Setting { Key = RegressionNotifier.WebhookSettingKey });
        setting.Value = string.IsNullOrEmpty(url) ? null : url;
        await db.SaveChangesAsync(ct);
        return new SettingsDto(setting.Value);
    }

    [HttpGet("/api/notifications")]
    public async Task<IReadOnlyList<NotificationDto>> Notifications(CancellationToken ct) =>
        (await db.Notifications.AsNoTracking().OrderByDescending(n => n.Id).Take(100).ToListAsync(ct)).Select(n => n.ToDto()).ToList();
}
