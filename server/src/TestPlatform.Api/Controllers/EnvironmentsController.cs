using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestPlatform.Api.Contracts;
using TestPlatform.Api.Data;
using TestPlatform.Api.Domain;
using TestPlatform.Api.Infrastructure;
using TestPlatform.Api.Services;

namespace TestPlatform.Api.Controllers;

/// <summary>TM-01 — target environments. Exactly zero or one environment is the default.</summary>
[ApiController]
[Route("api/environments")]
public class EnvironmentsController(PlatformDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<EnvironmentDto>> List(CancellationToken ct) =>
        (await db.Environments.AsNoTracking().OrderByDescending(e => e.IsDefault).ThenBy(e => e.Name).ToListAsync(ct))
        .Select(e => e.ToDto()).ToList();

    [HttpPost]
    [Authorize(Policy = AuthSetup.OperatorPolicy)]
    public async Task<ActionResult<EnvironmentDto>> Create(EnvironmentInput input, CancellationToken ct)
    {
        var env = new TargetEnvironment { CreatedAt = DateTimeOffset.UtcNow };
        await ApplyAsync(env, input, ct);
        db.Environments.Add(env);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { id = env.Id }, env.ToDto());
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthSetup.OperatorPolicy)]
    public async Task<EnvironmentDto> Update(int id, EnvironmentInput input, CancellationToken ct)
    {
        var env = await db.Environments.FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException($"Environment {id} not found");
        await ApplyAsync(env, input, ct);
        await db.SaveChangesAsync(ct);
        return env.ToDto();
    }

    [HttpPost("{id:int}/default")]
    [Authorize(Policy = AuthSetup.OperatorPolicy)]
    public async Task<EnvironmentDto> MakeDefault(int id, CancellationToken ct)
    {
        var env = await db.Environments.FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException($"Environment {id} not found");
        await ClearDefaultAsync(ct);
        env.IsDefault = true;
        env.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return env.ToDto();
    }

    /// <summary>Runs keep a snapshot of the environment (name + URL), so deleting it never changes history.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthSetup.OperatorPolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var env = await db.Environments.FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException($"Environment {id} not found");
        if (await db.Runs.AnyAsync(r => r.EnvironmentId == id && r.FinishedAt == null, ct))
            throw new ConflictException($"A run is active on {env.Name}. Stop it before deleting the environment.");
        db.Environments.Remove(env);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task ApplyAsync(TargetEnvironment env, EnvironmentInput input, CancellationToken ct)
    {
        var name = input.Name.Trim();
        var url = input.BaseUrl.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new BadRequestException("Base URL must be an absolute http(s) URL");
        if (!url.EndsWith('/')) url += "/";
        if (await db.Environments.AnyAsync(e => e.Id != env.Id && e.Name == name, ct))
            throw new ConflictException($"An environment named '{name}' already exists");
        var protectedIds = string.IsNullOrWhiteSpace(input.ProtectedIds) ? "1-11" : input.ProtectedIds.Trim();
        CleanupGuard.ParseProtectedIds(protectedIds); // validates the format (400 on error)

        if (input.IsDefault) await ClearDefaultAsync(ct);
        env.Name = name;
        env.BaseUrl = url;
        env.ProtectedIds = protectedIds;
        env.IsDefault = input.IsDefault || !await db.Environments.AnyAsync(e => e.Id != env.Id, ct); // the first one becomes default
        env.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task ClearDefaultAsync(CancellationToken ct)
    {
        foreach (var other in await db.Environments.Where(e => e.IsDefault).ToListAsync(ct)) other.IsDefault = false;
    }
}
