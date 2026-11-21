import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import type { EnvironmentSummary, TestCaseItem } from '../api/types';
import { Empty, ErrorBox, Loading, PageHeader, StatusPill } from '../components/ui';
import { formatDateTime, since, tcId } from '../lib/format';
import { useAsync } from '../lib/useAsync';
import { useResync } from '../realtime/hub';

/** TM-04 — every test case with its current state per environment, failing-since, last pass and flakiness. */
export function TestsPage() {
  const navigate = useNavigate();
  const { data, error, loading, reload } = useAsync(() => api.tests(), []);
  const [query, setQuery] = useState('');
  const [show, setShow] = useState<'all' | 'failing' | 'flaky' | 'known'>('all');
  useResync(() => void reload(), [reload]);

  const rows = useMemo(() => (data ?? []).filter((t) => {
    const text = `${t.title} ${t.tags} ${t.file}`.toLowerCase();
    if (query && !text.includes(query.toLowerCase())) return false;
    if (show === 'failing') return t.environments.some((e) => e.current === 'Fail' && e.currentStatus === 'Failed');
    if (show === 'known') return t.environments.some((e) => e.currentStatus === 'ExpectedFailure');
    if (show === 'flaky') return t.environments.some((e) => e.isFlaky);
    return true;
  }), [data, query, show]);

  return (
    <>
      <PageHeader title="Test history" subtitle="Each test across all runs. Open one to see when it started failing, when it last passed, and whether it is flaky." />
      <div className="toolbar">
        <input className="search" placeholder="Search title, TC id, tag (e.g. FR-04)…" value={query} onChange={(e) => setQuery(e.target.value)} />
        <div className="segmented" role="group" aria-label="Filter tests">
          {([['all', 'All'], ['failing', 'Failing unexpectedly'], ['known', 'Known bugs'], ['flaky', 'Flaky']] as const).map(([v, l]) => (
            <button key={v} className={show === v ? 'active' : ''} onClick={() => setShow(v)}>{l}</button>
          ))}
        </div>
      </div>
      <ErrorBox message={error} onRetry={reload} />
      {loading && !data ? <Loading /> : rows.length === 0 ? <Empty>No test matches.</Empty> : (
        <div className="card table-wrap">
          <table className="table">
            <thead><tr><th>TC</th><th>Test</th><th>Type</th><th>Current state per environment</th><th>Runs</th></tr></thead>
            <tbody>
              {rows.map((t) => (
                <tr key={t.id} className="clickable" onClick={() => navigate(`/tests/${t.id}`)}>
                  <td className="mono">{tcId(t.tags)}</td>
                  <td>{t.title.replace(/^TC-\d+\s*/, '')}</td>
                  <td>{t.project.toUpperCase()}</td>
                  <td><EnvStates envs={t.environments} /></td>
                  <td className="col-num">{t.executions}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}

export function EnvStates({ envs }: { envs: TestCaseItem['environments'] }) {
  if (!envs.length) return <span className="muted">Never run</span>;
  return (
    <div className="env-states">
      {envs.map((e) => <EnvState key={e.environmentName} e={e} />)}
    </div>
  );
}

function EnvState({ e }: { e: EnvironmentSummary }) {
  return (
    <div className="env-state">
      <span className="env-name">{e.environmentName}</span>
      {e.currentStatus && <StatusPill status={e.currentStatus} />}
      {e.isFlaky && <span className="pill warning" title={`Verdict changed ${e.flips} times recently with no code change`}>⚠ Flaky</span>}
      {e.current === 'Fail' && e.failingSince && (
        <small className="muted" title={formatDateTime(e.failingSince)}>failing for {since(e.failingSince)}</small>
      )}
      {e.current === 'Pass' && <small className="muted">last passed {formatDateTime(e.lastPassedAt)}</small>}
    </div>
  );
}
