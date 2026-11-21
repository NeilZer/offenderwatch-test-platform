import * as signalR from '@microsoft/signalr';
import { useEffect, useState } from 'react';

/**
 * One SignalR connection for the whole app (TM-03).
 *
 * Reliability model:
 *  - the hub only PUSHES changes; the REST API is the source of truth;
 *  - on reconnect we re-join our groups and emit "resync" so every open screen reloads its data
 *    from the API — whatever happened while we were offline is picked up, nothing is lost;
 *  - the connection state is visible in the UI (Live / Reconnecting / Offline).
 */
export type HubState = 'connecting' | 'live' | 'reconnecting' | 'offline';

type Handler = (...args: never[]) => void;

class RunsHub {
  private connection: signalR.HubConnection | null = null;
  private state: HubState = 'offline';
  private readonly stateListeners = new Set<(s: HubState) => void>();
  private readonly resyncListeners = new Set<() => void>();
  private readonly watchedRuns = new Map<number, number>(); // runId → number of screens watching
  /** Handlers live here (not on the connection) so screens can subscribe before the connection exists. */
  private readonly handlers = new Map<string, Set<Handler>>();
  private readonly boundEvents = new Set<string>();

  async start(): Promise<void> {
    if (this.connection) return;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/runs')
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (ctx) => Math.min(1000 * 2 ** ctx.previousRetryCount, 10_000) })
      .configureLogging(signalR.LogLevel.Warning)
      .build();
    this.connection = connection;
    this.boundEvents.clear();
    for (const event of this.handlers.keys()) this.bind(event);

    connection.onreconnecting(() => this.setState('reconnecting'));
    connection.onreconnected(async () => {
      await this.joinGroups();
      this.setState('live');
      this.resyncListeners.forEach((l) => l());
    });
    connection.onclose(() => {
      this.setState('offline');
      // Automatic reconnect gave up (e.g. server down for a while): keep trying in the background.
      if (this.connection === connection) setTimeout(() => this.restart(), 5000);
    });
    await this.connect();
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    this.watchedRuns.clear();
    if (connection) await connection.stop();
    this.setState('offline');
  }

  on(event: string, handler: Handler): () => void {
    if (!this.handlers.has(event)) this.handlers.set(event, new Set());
    this.handlers.get(event)!.add(handler);
    this.bind(event);
    return () => this.handlers.get(event)?.delete(handler);
  }

  /** One dispatcher per event on the connection, fanning out to every subscribed screen. */
  private bind(event: string): void {
    if (!this.connection || this.boundEvents.has(event)) return;
    this.boundEvents.add(event);
    this.connection.on(event, (...args: unknown[]) => {
      this.handlers.get(event)?.forEach((h) => (h as (...a: unknown[]) => void)(...args));
    });
  }

  watchRun(runId: number): () => void {
    this.watchedRuns.set(runId, (this.watchedRuns.get(runId) ?? 0) + 1);
    if (this.state === 'live') void this.connection?.invoke('WatchRun', runId).catch(() => undefined);
    return () => {
      const count = (this.watchedRuns.get(runId) ?? 1) - 1;
      if (count > 0) this.watchedRuns.set(runId, count);
      else {
        this.watchedRuns.delete(runId);
        if (this.state === 'live') void this.connection?.invoke('UnwatchRun', runId).catch(() => undefined);
      }
    };
  }

  onState(listener: (s: HubState) => void): () => void {
    this.stateListeners.add(listener);
    listener(this.state);
    return () => this.stateListeners.delete(listener);
  }

  onResync(listener: () => void): () => void {
    this.resyncListeners.add(listener);
    return () => this.resyncListeners.delete(listener);
  }

  private async connect(): Promise<void> {
    if (!this.connection) return;
    this.setState('connecting');
    try {
      await this.connection.start();
      await this.joinGroups();
      this.setState('live');
      this.resyncListeners.forEach((l) => l());
    } catch {
      this.setState('offline');
      setTimeout(() => this.restart(), 5000);
    }
  }

  private async restart(): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Disconnected) return;
    await this.connect();
  }

  private async joinGroups(): Promise<void> {
    if (!this.connection) return;
    await this.connection.invoke('WatchRuns');
    for (const runId of this.watchedRuns.keys()) await this.connection.invoke('WatchRun', runId);
  }

  private setState(state: HubState): void {
    this.state = state;
    this.stateListeners.forEach((l) => l(state));
  }
}

export const runsHub = new RunsHub();

export function useHubState(): HubState {
  const [state, setState] = useState<HubState>('offline');
  useEffect(() => runsHub.onState(setState), []);
  return state;
}

/** Subscribes to a hub event for the lifetime of the component. */
export function useHubEvent<T extends unknown[]>(event: string, handler: (...args: T) => void, deps: unknown[] = []): void {
  useEffect(() => runsHub.on(event, handler as unknown as Handler), [event, ...deps]); // eslint-disable-line react-hooks/exhaustive-deps
}

/** Called after (re)connecting — reload screen data from the API. */
export function useResync(handler: () => void, deps: unknown[] = []): void {
  useEffect(() => runsHub.onResync(handler), deps); // eslint-disable-line react-hooks/exhaustive-deps
}
