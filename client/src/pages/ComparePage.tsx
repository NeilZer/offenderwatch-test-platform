import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { api } from '../api/client';
import type { ComparisonRow, RunSummary } from '../api/types';
import { Empty, ErrorBox, Loading, PageHeader, StatusPill, TotalsInline } from '../components/ui';
import { formatDateTime } from '../lib/format';
import { useAsync } from '../lib/useAsync';

/** B-02 — what changed between two runs: the view a QA lead opens before approving a release. */
export function ComparePage() {
  const [params, setParams] = useSearchParams();
  const runs = useAsync(() => api.runs(), []);
  const baseId = Number(params.get('base')) || null;
  const targetId = Number(params.get('target')) || null;
  const [error, setError] = useState<string | null>(null);
  const comparison = useAsync(() => (baseId && targetId ? api.compare(baseId, targetId) : Promise.resolve(null)), [baseId, targetId]);

  useEffect(() => setError(comparison.error), [comparison.error]);

  const choose = (key: 'base' | 'target', value: string) => {
    const next = new URLSearchParams(params);
    next.set(key, value);
    setParams(next);
  };

  const finished = (runs.data ?? []).filter((r) => r.finishedAt);
  const c = comparison.data;

  return (
    <>
      <PageHeader title="Compare runs" subtitle="Pick a baseline and a newer run. Regressions and recoveries are listed first." />
      <div className="card compare-pick">
        <RunSelect label="Baseline (before)" runs={finished} value={baseId} onChange={(v) => choose('base', v)} />
        <span className="arrow">→</span>
        <RunSelect label="Compared run (after)" runs={finished} value={targetId} onChange={(v) => choose('target', v)} />
      </div>
      <ErrorBox message={error} />
      {!baseId || !targetId ? <Empty>Choose two runs to compare.</Empty> : comparison.loading && !c ? <Loading /> : c && (
        <>
          <div className="grid-cards">
            <Stat label="Regressed" value={c.regressed.length} tone="critical" help="Passed before, fail now" />
            <Stat label="Recovered" value={c.recovered.length} tone="good" help="Failed before, pass now" />
            <Stat label="Status changed" value={c.statusChanged.length} tone="info" help="Same pass/fail verdict, different status (e.g. known bug → unexpected failure)" />
            <Stat label="New / missing" value={`${c.new.length} / ${c.missing.length}`} tone="neutral" help="Tests only in one of the two runs" />
            <Stat label="Unchanged" value={c.unchanged} tone="neutral" help="Same status in both runs" />
          </div>
          <section className="card">
            <h2>Totals</h2>
            <table className="table compact">
              <thead><tr><th /><th>Run</th><th>Environment</th><th>Finished</th><th>Results</th></tr></thead>
              <tbody>
                <tr><td>Before</td><td><Link to={`/runs/${c.base.id}`}>#{c.base.id}</Link></td><td>{c.base.environmentName}</td><td>{formatDateTime(c.base.finishedAt)}</td><td><TotalsInline run={c.base} /></td></tr>
                <tr><td>After</td><td><Link to={`/runs/${c.target.id}`}>#{c.target.id}</Link></td><td>{c.target.environmentName}</td><td>{formatDateTime(c.target.finishedAt)}</td><td><TotalsInline run={c.target} /></td></tr>
                <tr className="delta"><td>Change</td><td colSpan={3} /><td>
                  {delta('passed', c.delta.passed)} {delta('known bug', c.delta.expectedFailures)} {delta('failed', c.delta.failed)} {delta('skipped', c.delta.skipped)}
                </td></tr>
              </tbody>
            </table>
          </section>
          <Section title="Regressed (pass → fail)" rows={c.regressed} />
          <Section title="Recovered (fail → pass)" rows={c.recovered} />
          <Section title="Status changed" rows={c.statusChanged} />
          <Section title="New in the compared run" rows={c.new} />
          <Section title="Missing from the compared run" rows={c.missing} />
        </>
      )}
    </>
  );
}

function RunSelect({ label, runs, value, onChange }: { label: string; runs: RunSummary[]; value: number | null; onChange: (v: string) => void }) {
  return (
    <label>
      {label}
      <select value={value ?? ''} onChange={(e) => onChange(e.target.value)}>
        <option value="">— choose a run —</option>
        {runs.map((r) => <option key={r.id} value={r.id}>#{r.id} · {r.environmentName} · {r.scope} · {formatDateTime(r.finishedAt)} · {r.status}</option>)}
      </select>
    </label>
  );
}

function Stat({ label, value, tone, help }: { label: string; value: number | string; tone: string; help: string }) {
  return <div className={`card stat tone-${tone}`} title={help}><span className="muted">{label}</span><b>{value}</b><small className="muted">{help}</small></div>;
}

function Section({ title, rows }: { title: string; rows: ComparisonRow[] }) {
  if (!rows.length) return null;
  return (
    <section className="card">
      <h2>{title} <span className="count">{rows.length}</span></h2>
      <table className="table">
        <thead><tr><th>Test</th><th>Type</th><th>Before</th><th>After</th></tr></thead>
        <tbody>{rows.map((r) => (
          <tr key={r.testCaseId}>
            <td><Link to={`/tests/${r.testCaseId}`}>{r.title}</Link></td><td>{r.project.toUpperCase()}</td>
            <td>{r.baseStatus ? <StatusPill status={r.baseStatus} /> : <span className="muted">not in run</span>}</td>
            <td>{r.targetStatus ? <StatusPill status={r.targetStatus} /> : <span className="muted">not in run</span>}</td>
          </tr>
        ))}</tbody>
      </table>
    </section>
  );
}

const delta = (label: string, n: number) => <span className={`t ${n === 0 ? 'muted' : ''}`}>{n > 0 ? '+' : ''}{n} {label}</span>;
