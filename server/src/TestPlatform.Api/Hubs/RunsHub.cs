using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TestPlatform.Api.Contracts;

namespace TestPlatform.Api.Hubs;

/// <summary>
/// Real-time channel (TM-03). Clients join:
///   "runs"        — every run's summary (runs list, dashboard)
///   "run-{id}"    — scenario-level updates + runner console output of one run
/// The hub only PUSHES changes. The database stays the source of truth: after a reconnect the client
/// re-joins and reloads the run over REST, so nothing missed while offline is lost.
/// </summary>
[Authorize]
public class RunsHub : Hub
{
    public Task WatchRuns() => Groups.AddToGroupAsync(Context.ConnectionId, RunEvents.AllRunsGroup);

    public Task WatchRun(int runId) => Groups.AddToGroupAsync(Context.ConnectionId, RunEvents.RunGroup(runId));

    public Task UnwatchRun(int runId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, RunEvents.RunGroup(runId));
}

/// <summary>Server → client messages. Names are the client-side event names.</summary>
public class RunEvents(IHubContext<RunsHub> hub)
{
    public const string AllRunsGroup = "runs";
    public static string RunGroup(int runId) => $"run-{runId}";

    public Task RunUpdated(RunSummaryDto run) =>
        Task.WhenAll(
            hub.Clients.Group(AllRunsGroup).SendAsync("runUpdated", run),
            hub.Clients.Group(RunGroup(run.Id)).SendAsync("runUpdated", run));

    public Task ScenarioUpdated(ScenarioDto scenario) =>
        hub.Clients.Group(RunGroup(scenario.RunId)).SendAsync("scenarioUpdated", scenario);

    public Task RunOutput(int runId, IReadOnlyList<string> lines) =>
        hub.Clients.Group(RunGroup(runId)).SendAsync("runOutput", runId, lines);

    public Task TestDataChanged() =>
        hub.Clients.Group(AllRunsGroup).SendAsync("testDataChanged");
}
