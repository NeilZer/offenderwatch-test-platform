using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TestPlatform.Api.Data;
using TestPlatform.Api.Domain;

namespace TestPlatform.Api.Services;

/// <summary>
/// B-03: after a run, notify about NEW unexpected failures — scenarios that are Failed (unexpected) now
/// and Passed the previous time they ran on the SAME environment (the previous run that actually
/// executed that test — an API-only run in between does not hide a UI regression). Expected failures (documented bugs)
/// never trigger it. Every notification is stored (audit trail, visible in the UI) and, when a webhook
/// URL is configured, POSTed as {"text": "..."} which both Slack and Teams incoming webhooks accept.
/// </summary>
public class RegressionNotifier(PlatformDbContext db, IHttpClientFactory http, ILogger<RegressionNotifier> logger)
{
    public const string WebhookSettingKey = "webhookUrl";

    public async Task<Notification?> OnRunFinishedAsync(int runId, CancellationToken ct)
    {
        var run = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null || run.Status != RunStatus.Completed) return null;
        if (await db.Notifications.AnyAsync(n => n.RunId == runId, ct)) return null; // once per run

        var newFailures = await FindNewFailuresAsync(run, ct);
        if (newFailures.Count == 0) return null;

        var text = BuildMessage(run, newFailures);
        var webhook = (await db.Settings.FindAsync([WebhookSettingKey], ct))?.Value;
        var notification = new Notification
        {
            RunId = runId,
            CreatedAt = DateTimeOffset.UtcNow,
            Summary = $"{newFailures.Count} new failure(s) on {run.EnvironmentName} in run #{runId}",
            Payload = text,
            Target = string.IsNullOrWhiteSpace(webhook) ? null : webhook,
        };
        if (!string.IsNullOrWhiteSpace(webhook))
        {
            try
            {
                var body = new StringContent(JsonSerializer.Serialize(new { text }), Encoding.UTF8, "application/json");
                var response = await http.CreateClient("webhook").PostAsync(webhook, body, ct);
                notification.Delivered = response.IsSuccessStatusCode;
                notification.DeliveryError = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}";
            }
            catch (Exception ex)
            {
                notification.DeliveryError = ex.Message;
                logger.LogWarning(ex, "Webhook delivery failed for run {RunId}", runId);
            }
        }
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
        return notification;
    }

    /// <summary>Titles of tests that FAILED UNEXPECTEDLY in this run and PASSED the previous time they ran here.</summary>
    public async Task<List<string>> FindNewFailuresAsync(Run run, CancellationToken ct)
    {
        var failed = await db.ScenarioResults.AsNoTracking()
            .Where(r => r.RunId == run.Id && r.Status == ScenarioStatus.Failed)
            .OrderBy(r => r.Ordinal)
            .Select(r => new { r.TestCaseId, r.TestCase!.Title }).ToListAsync(ct);
        var titles = new List<string>();
        foreach (var f in failed)
        {
            // The previous EXECUTED result of this test on this environment (skipped/stopped ones don't count).
            var previous = await db.ScenarioResults.AsNoTracking()
                .Where(r => r.TestCaseId == f.TestCaseId && r.RunId < run.Id && r.Run!.EnvironmentId == run.EnvironmentId
                            && r.Status != ScenarioStatus.Skipped && r.Status != ScenarioStatus.Interrupted && r.FinishedAt != null)
                .OrderByDescending(r => r.RunId).Select(r => (ScenarioStatus?)r.Status).FirstOrDefaultAsync(ct);
            if (previous == ScenarioStatus.Passed) titles.Add(f.Title);
        }
        return titles;
    }

    private static string BuildMessage(Run run, List<string> failures)
    {
        var lines = new StringBuilder();
        lines.AppendLine($":rotating_light: New regression on {run.EnvironmentName} — run #{run.Id}");
        lines.AppendLine($"{failures.Count} test(s) passed in the previous run and now fail unexpectedly:");
        foreach (var title in failures.Take(15)) lines.AppendLine($"• {title}");
        if (failures.Count > 15) lines.AppendLine($"… and {failures.Count - 15} more");
        return lines.ToString();
    }
}
