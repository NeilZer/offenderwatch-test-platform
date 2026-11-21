import { ReactNode, useEffect } from 'react';
import type { KnownBug, RunSummary, ScenarioStatus } from '../api/types';
import { RUN_STATUS_TONE, STATUS_META, passRate } from '../lib/format';
import { HubState, useHubState } from '../realtime/hub';

/** Small building blocks shared by every page. */

export function StatusPill({ status }: { status: ScenarioStatus }) {
  const meta = STATUS_META[status];
  return (
    <span className={`pill ${meta.tone}`} title={meta.help}>
      <i aria-hidden="true">{meta.icon}</i>
      {meta.label}
    </span>
  );
}

export function RunStatusPill({ run }: { run: RunSummary }) {
  const tone = RUN_STATUS_TONE[run.status] ?? 'neutral';
  const icon = run.status === 'Running' || run.status === 'Queued' ? '◌' : run.status === 'Completed' ? '✓' : run.status === 'Stopped' ? '■' : '!';
  return (
    <span className={`pill ${tone}`} title={run.statusReason ?? run.status}>
      <i aria-hidden="true">{icon}</i>
      {run.status}
    </span>
  );
}

export function BugTags({ bugs }: { bugs: KnownBug[] }) {
  if (!bugs.length) return null;
  return (
    <span className="bugs">
      {bugs.map((b) => (
        <span key={b.id} className={`bug sev-${(b.severity ?? 'unknown').toLowerCase()}`} title={`${b.id} — ${b.title}${b.severity ? ` (${b.severity})` : ''}`}>
          {b.id}
        </span>
      ))}
    </span>
  );
}

/**
 * Segmented bar of a run's results: passed · known bug · failed · bug fixed? · skipped · still to run.
 * Expected failures are visibly different from unexpected ones (TM 5.2).
 */
export function TotalsBar({ run, showLegend = true }: { run: RunSummary; showLegend?: boolean }) {
  const t = run.totals;
  const remaining = Math.max(0, t.total - run.completed);
  const parts = [
    { key: 'passed', label: 'Passed', value: t.passed, tone: 'good' },
    { key: 'expected', label: 'Known bug', value: t.expectedFailures, tone: 'serious' },
    { key: 'failed', label: 'Failed (unexpected)', value: t.failed, tone: 'critical' },
    { key: 'unexpectedPass', label: 'Bug fixed?', value: t.unexpectedPasses, tone: 'info' },
    { key: 'skipped', label: 'Skipped / stopped', value: t.skipped, tone: 'neutral' },
    { key: 'remaining', label: 'Still to run', value: remaining, tone: 'track' },
  ];
  const total = Math.max(1, parts.reduce((s, p) => s + p.value, 0));
  return (
    <div>
      <div className="segbar" role="img" aria-label={parts.map((p) => `${p.label} ${p.value}`).join(', ')}>
        {parts.filter((p) => p.value > 0).map((p) => (
          <div key={p.key} className={`seg ${p.tone}`} style={{ flex: p.value }} title={`${p.label}: ${p.value} (${Math.round((p.value / total) * 100)}%)`} />
        ))}
      </div>
      {showLegend && (
        <ul className="legend">
          {parts.filter((p) => p.value > 0 || p.key === 'failed').map((p) => (
            <li key={p.key}><i className={`sw ${p.tone}`} />{p.label} <b>{p.value}</b></li>
          ))}
        </ul>
      )}
    </div>
  );
}

/** Totals as compact numbers for tables: ✓ passed · ✕ known bug · ! failed · – skipped */
export function TotalsInline({ run }: { run: RunSummary }) {
  const t = run.totals;
  return (
    <span className="totals-inline">
      <span className="t good" title="Passed">✓ {t.passed}</span>
      <span className="t serious" title="Known bug (expected failure)">✕ {t.expectedFailures}</span>
      <span className={`t ${t.failed ? 'critical' : 'muted'}`} title="Failed unexpectedly">! {t.failed}</span>
      {t.unexpectedPasses > 0 && <span className="t info" title="Expected to fail but passed — retest the bug">↻ {t.unexpectedPasses}</span>}
      <span className="t muted" title="Skipped / stopped">– {t.skipped}</span>
    </span>
  );
}

export function runPassRate(run: RunSummary): number {
  const t = run.totals;
  return passRate(t.passed + t.unexpectedPasses, t.passed + t.unexpectedPasses + t.failed + t.expectedFailures);
}

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: ReactNode; actions?: ReactNode }) {
  return (
    <header className="page-header">
      <div>
        <h1>{title}</h1>
        {subtitle && <p className="muted">{subtitle}</p>}
      </div>
      {actions && <div className="actions">{actions}</div>}
    </header>
  );
}

export function ErrorBox({ message, onRetry }: { message: string | null; onRetry?: () => void }) {
  if (!message) return null;
  return (
    <div className="error-box" role="alert">
      <span>{message}</span>
      {onRetry && <button className="btn small" onClick={onRetry}>Retry</button>}
    </div>
  );
}

export function Empty({ children }: { children: ReactNode }) {
  return <div className="empty">{children}</div>;
}

export function Loading() {
  return <div className="empty"><span className="spinner" /> Loading…</div>;
}

const HUB_LABEL: Record<HubState, string> = { live: 'Live', connecting: 'Connecting…', reconnecting: 'Reconnecting…', offline: 'Offline' };

export function LiveIndicator() {
  const state = useHubState();
  return (
    <span className={`live live-${state}`} title="Real-time connection to the platform (SignalR)">
      <i aria-hidden="true" />{HUB_LABEL[state]}
    </span>
  );
}

export function Modal({ title, onClose, children, wide = false }: { title: string; onClose: () => void; children: ReactNode; wide?: boolean }) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);
  return (
    <div className="backdrop" onMouseDown={onClose}>
      <div className={`modal ${wide ? 'wide' : ''}`} role="dialog" aria-modal="true" aria-label={title} onMouseDown={(e) => e.stopPropagation()}>
        <div className="modal-head">
          <h2>{title}</h2>
          <button className="icon-btn" onClick={onClose} aria-label="Close">✕</button>
        </div>
        {children}
      </div>
    </div>
  );
}

export function Drawer({ title, onClose, children }: { title: ReactNode; onClose: () => void; children: ReactNode }) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);
  return (
    <div className="backdrop drawer-backdrop" onMouseDown={onClose}>
      <aside className="drawer" role="dialog" aria-modal="true" onMouseDown={(e) => e.stopPropagation()}>
        <div className="modal-head">
          <div className="drawer-title">{title}</div>
          <button className="icon-btn" onClick={onClose} aria-label="Close">✕</button>
        </div>
        <div className="drawer-body">{children}</div>
      </aside>
    </div>
  );
}

/** Disabled-with-reason button for viewers (B-07). */
export function OperatorButton({ canOperate, children, className = 'btn', ...rest }:
  { canOperate: boolean } & React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button {...rest} className={className} disabled={!canOperate || rest.disabled}
      title={canOperate ? rest.title : 'Operators only — you are signed in as a viewer'}>
      {children}
    </button>
  );
}
