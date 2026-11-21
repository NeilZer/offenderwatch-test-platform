using Cronos;
using Microsoft.EntityFrameworkCore;
using TestPlatform.Api.Contracts;
using TestPlatform.Api.Data;
using TestPlatform.Api.Domain;
using TestPlatform.Api.Infrastructure;

namespace TestPlatform.Api.Services;

/// <summary>B-04 — CRUD for recurring runs. Cron is standard 5-field ("0 2 * * *" = every day at 02:00).</summary>
public class ScheduleService(PlatformDbContext db)
{
    public async Task<IReadOnlyList<ScheduleDto>> ListAsync(CancellationToken ct) =>
        (await db.Schedules.AsNoTracking().Include(s => s.Environment).OrderBy(s => s.Name).ToListAsync(ct)).Select(s => s.ToDto()).ToList();

    public async Task<ScheduleDto> CreateAsync(ScheduleInput input, CancellationToken ct)
    {
        var schedule = new Schedule { CreatedAt = DateTimeOffset.UtcNow };
        await ApplyAsync(schedule, input, ct);
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync(ct);
        return (await db.Schedules.AsNoTracking().Include(s => s.Environment).FirstAsync(s => s.Id == schedule.Id, ct)).ToDto();
    }

    public async Task<ScheduleDto> UpdateAsync(int id, ScheduleInput input, CancellationToken ct)
    {
        var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException($"Schedule {id} not found");
        await ApplyAsync(schedule, input, ct);
        await db.SaveChangesAsync(ct);
        return (await db.Schedules.AsNoTracking().Include(s => s.Environment).FirstAsync(s => s.Id == id, ct)).ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException($"Schedule {id} not found");
        db.Schedules.Remove(schedule);
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(Schedule schedule, ScheduleInput input, CancellationToken ct)
    {
        if (!await db.Environments.AnyAsync(e => e.Id == input.EnvironmentId, ct)) throw new BadRequestException($"Environment {input.EnvironmentId} not found");
        var zone = ResolveZone(input.TimeZone);
        var cron = ParseCron(input.Cron);
        schedule.Name = input.Name.Trim();
        schedule.Cron = input.Cron.Trim();
        schedule.TimeZone = zone.Id;
        schedule.EnvironmentId = input.EnvironmentId;
        schedule.Scope = string.IsNullOrWhiteSpace(input.Scope) ? "all" : input.Scope;
        schedule.Enabled = input.Enabled;
        schedule.NextRunAt = input.Enabled ? cron.GetNextOccurrence(DateTimeOffset.UtcNow, zone) : null;
    }

    public static CronExpression ParseCron(string expression)
    {
        try { return CronExpression.Parse(expression.Trim()); }
        catch (CronFormatException ex) { throw new BadRequestException($"Invalid cron expression '{expression}': {ex.Message}"); }
    }

    public static TimeZoneInfo ResolveZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id.Trim()); }
        catch (TimeZoneNotFoundException) { throw new BadRequestException($"Unknown time zone '{id}'"); }
    }
}

/// <summary>
/// Fires due schedules. State lives in SQLite (NextRunAt), so schedules survive an API restart.
/// Policy for occurrences missed while the API was down: they are skipped (no burst of catch-up runs);
/// the next occurrence is computed from "now".
/// </summary>
public class ScheduleRunner(IServiceScopeFactory scopes, ILogger<ScheduleRunner> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SkipMissedOccurrencesAsync(stoppingToken);
        using var timer = new PeriodicTimer(Tick);
        do
        {
            try
            {
                await FireDueSchedulesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Scheduler tick failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SkipMissedOccurrencesAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var now = DateTimeOffset.UtcNow;
        var missed = await db.Schedules.Where(s => s.Enabled).ToListAsync(ct);
        foreach (var schedule in missed.Where(s => s.NextRunAt is null || s.NextRunAt < now.AddSeconds(-30)))
        {
            schedule.NextRunAt = ScheduleService.ParseCron(schedule.Cron).GetNextOccurrence(now, ScheduleService.ResolveZone(schedule.TimeZone));
            logger.LogInformation("Schedule '{Name}': missed occurrence skipped, next at {Next}", schedule.Name, schedule.NextRunAt);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task FireDueSchedulesAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<RunOrchestrator>();
        var now = DateTimeOffset.UtcNow;
        var due = (await db.Schedules.Where(s => s.Enabled && s.NextRunAt != null).ToListAsync(ct)).Where(s => s.NextRunAt <= now).ToList();

        foreach (var schedule in due)
        {
            try
            {
                var run = await orchestrator.StartAsync(
                    new StartRunRequest(schedule.EnvironmentId, schedule.Scope, null, RunTrigger.Scheduled, $"schedule: {schedule.Name}", schedule.Id), ct);
                schedule.LastRunId = run.Id;
                logger.LogInformation("Schedule '{Name}' started run {RunId}", schedule.Name, run.Id);
            }
            catch (ConflictException ex)
            {
                logger.LogWarning("Schedule '{Name}' skipped: {Reason}", schedule.Name, ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Schedule '{Name}' failed to start a run", schedule.Name);
            }
            schedule.LastRunAt = now;
            schedule.NextRunAt = ScheduleService.ParseCron(schedule.Cron).GetNextOccurrence(now, ScheduleService.ResolveZone(schedule.TimeZone));
        }
        if (due.Count > 0) await db.SaveChangesAsync(ct);
    }
}
