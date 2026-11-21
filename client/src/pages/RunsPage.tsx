import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import type { RunSummary } from '../api/types';
import { useCanOperate } from '../auth/AuthContext';
import { StartRunDialog } from '../components/StartRunDialog';
import { Empty, ErrorBox, Loading, OperatorButton, PageHeader, RunStatusPill, TotalsInline, runPassRate } from '../components/ui';
import { formatDateTime, formatDuration, scopeLabel } from '../lib/format';
import { useAsync } from '../lib/useAsync';
import { useHubEvent, useResync } from '../realtime/hub';

/** TM-02 — every run with environment, start / end, duration, trigger and totals. B-02 — pick two to compare. */
export function RunsPage() {
  const canOperate = useCanOperate();
  const navigate = useNavigate();
  const { data: runs, setData, error, loading, reload } = useAsync(() => api.runs(), []);
  const [starting, setStarting] = useState(false);
  const [compare, setCompare] = useState<number[]>([]);

  useHubEvent<[RunSummary]>('runUpdated', (run) => {
    setData((list) => {
      if (!list) return list;
      return list.some((r) => r.id === run.id) ? list.map((r) => (r.id === run.id ? run : r)) : [run, ...list];
    });
  }, []);
  useResync(() => void reload(), [reload]);

  function toggleCompare(id: number) {
    setCompare((c) => (c.includes(id) ? c.filter((x) => x !== id) : [...c, id].slice(-2)));
  }

  return (
    <>
      <PageHeader
        title="Test runs"
        subtitle="Every execution of the suite. Open a run to watch it live or inspect its results."
        actions={<>
          <button className="btn" disabled={compare.length !== 2} onClick={() => {
            const [a, b] = [...compare].sort((x, y) => x - y);
            navigate(`/compare?base=${a}&target=${b}`);
          }} title="Tick two runs, then compare them">⇄ Compare {compare.length}/2</button>
          <OperatorButton canOperate={canOperate} className="btn primary" onClick={() => setStarting(true)}>▶ Start run</OperatorButton>
        </>}
      />
      <ErrorBox message={error} onRetry={reload} />
      {loading && !runs ? <Loading /> : !runs?.length ? (
        <Empty>No runs yet. {canOperate ? 'Click “Start run”.' : 'An operator can start one.'}</Empty>
      ) : (
        <div className="card table-wrap">
          <table className="table runs">
            <thead>
              <tr>
                <th title="Select two runs to compare">⇄</th><th>Run</th><th>Environment</th><th>Status</th><th>Trigger</th>
                <th>Started</th><th>Ended</th><th>Duration</th><th>Results <small className="muted">✓ passed · ✕ known bug · ! failed · – skipped</small></th><th>Pass rate</th>
              </tr>
            </thead>
            <tbody>
              {runs.map((run) => (
                <tr key={run.id} className="clickable" onClick={() => navigate(`/runs/${run.id}`)}>
                  <td onClick={(e) => e.stopPropagation()}>
                    <input type="checkbox" aria-label={`Select run ${run.id} for comparison`} checked={compare.includes(run.id)} onChange={() => toggleCompare(run.id)} />
                  </td>
                  <td><Link to={`/runs/${run.id}`} onClick={(e) => e.stopPropagation()}><b>#{run.id}</b></Link></td>
                  <td>{run.environmentName}</td>
                  <td><RunStatusPill run={run} /></td>
                  <td>{run.trigger}{!run.isFullSuite && <small className="muted"> · {scopeLabel(run)}</small>}</td>
                  <td className="nowrap">{formatDateTime(run.startedAt ?? run.createdAt)}</td>
                  <td className="nowrap">{formatDateTime(run.finishedAt)}</td>
                  <td>{formatDuration(run.durationMs)}</td>
                  <td>
                    <TotalsInline run={run} />
                    {!run.finishedAt && <small className="muted"> ({run.completed}/{run.totals.total})</small>}
                  </td>
                  <td>{run.completed ? `${runPassRate(run)}%` : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {starting && <StartRunDialog onClose={() => setStarting(false)} />}
    </>
  );
}
