import type { ScenarioStatus } from '../api/types';

/** Dates follow the OffenderWatch convention: DD/MM/YYYY, 24-hour time, viewer's local timezone. */
export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}

export function formatTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

export function formatDuration(ms: number | null | undefined): string {
  if (ms === null || ms === undefined) return '—';
  if (ms < 1000) return `${ms} ms`;
  const s = Math.round(ms / 1000);
  if (s < 60) return `${s}s`;
  const m = Math.floor(s / 60);
  return m < 60 ? `${m}m ${s % 60}s` : `${Math.floor(m / 60)}h ${m % 60}m`;
}

/** "3 days", "2 hours", "5 minutes" — how long since a moment. */
export function since(iso: string | null | undefined, now = Date.now()): string {
  if (!iso) return '—';
  const minutes = Math.max(0, Math.round((now - Date.parse(iso)) / 60000));
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.round(minutes / 60);
  if (hours < 48) return `${hours} h`;
  return `${Math.round(hours / 24)} days`;
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

/** Plain-language status for a non-technical reader, plus the CSS tone. */
export const STATUS_META: Record<ScenarioStatus, { label: string; tone: string; icon: string; help: string }> = {
  Queued: { label: 'Queued', tone: 'neutral', icon: '•', help: 'Waiting to run' },
  Running: { label: 'Running', tone: 'running', icon: '◌', help: 'Executing right now' },
  Passed: { label: 'Passed', tone: 'good', icon: '✓', help: 'The requirement works' },
  Failed: { label: 'Failed', tone: 'critical', icon: '!', help: 'Unexpected failure — a new problem that needs attention' },
  ExpectedFailure: { label: 'Known bug', tone: 'serious', icon: '✕', help: 'Fails because of a documented bug (expected failure)' },
  UnexpectedPass: { label: 'Bug fixed?', tone: 'info', icon: '↻', help: 'Was expected to fail but passed — retest the bug' },
  Skipped: { label: 'Skipped', tone: 'neutral', icon: '–', help: 'Not executed' },
  Interrupted: { label: 'Stopped', tone: 'neutral', icon: '■', help: 'The run was stopped before this scenario finished' },
};

export const RUN_STATUS_TONE: Record<string, string> = {
  Queued: 'neutral', Running: 'running', Completed: 'good', Stopped: 'neutral', Error: 'critical',
};

/** "Full suite", "API only", "UI only · filter TC-21" — partial runs are always labelled. */
export function scopeLabel(run: { scope: string; filter: string | null }): string {
  const scope = run.scope === 'all' ? 'Full suite' : run.scope === 'api' ? 'API only' : run.scope === 'ui' ? 'UI only' : run.scope;
  return run.filter ? `${scope} · filter ${run.filter}` : scope;
}

export function tcId(tags: string): string {
  return tags.split(' ').find((t) => /^TC-\d+$/.test(t)) ?? '';
}

export function passRate(passed: number, executed: number): number {
  return executed === 0 ? 0 : Math.round((passed / executed) * 100);
}
