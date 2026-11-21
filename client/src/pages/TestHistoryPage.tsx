import { useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../api/client';
import type { HistoryEntry, Scenario } from '../api/types';
import { EvidencePanel } from '../components/EvidencePanel';
import { BugTags, Drawer, Empty, ErrorBox, Loading, PageHeader, StatusPill } from '../components/ui';
import { STATUS_META, formatDateTime, formatDuration, since } from '../lib/format';
import { useAsync } from '../lib/useAsync';
import { useResync } from '../realtime/hub';

/**
 * TM-04 — one test across all runs, oldest → newest.
 * Answers at a glance: when did it start failing? when did it last pass? is it flaky?
 * Transitions are explicit: ↓ Regression (pass → fail), ↑ Recovery (fail → pass).
 */
export function TestHistoryPage() {
  const id = Number(useParams().id);
  const { data, error, loading, reload } = useAsync(() => api.testHistory(id), [id]);
  const [env, setEnv] = useState<string>('all');
  const [evidence, setEvidence] = useState<Scenario | null>(null);
  const [evidenceError, setEvidenceError] = useState<string | null>(null);
  useResync(() => void reload(), [reload]);

  const environments = useMemo(() => [...new Set((data?.entries ?? []).map((e) => e.environmentName))], [data]);
  const entries = useMemo(() => (data?.entries ?? []).filter((e) => env === 'all' || e.environmentName === env), [data, env]);

  async function openEvidence(entry: HistoryEntry) {
    setEvidenceError(null);
    try { setEvidence(await api.result(entry.resultId)); } catch (e) { setEvidenceError((e as Error).message); }
  }

  if (loading && !data) return <Loading />;
  if (error && !data) return <ErrorBox message={error} onRetry={reload} />;
  if (!data) return null;
  const t = data.testCase;

  return (
    <>
      <PageHeader title={t.title} subtitle={<><code>{t.file}</code> · {t.project.toUpperCase()} · tags {t.tags || '—'}</>}
        actions={<Link className="btn" to="/tests">← All tests</Link>} />
      <ErrorBox message={evidenceError} />

      <section className="summary-cards">
        {t.environments.length === 0 && <Empty>This test has not run yet.</Empty>}
        {t.environments.map((e) => (
          <div key={e.environmentName} className={`card answer ${e.current === 'Fail' ? (e.currentStatus === 'ExpectedFailure' ? 'tone-serious' : 'tone-critical') : 'tone-good'}`}>
            <h3>{e.environmentName}</h3>
            <dl className="answers">
              <dt>Now</dt><dd>{e.currentStatus ? <StatusPill status={e.currentStatus} /> : '—'}</dd>
              <dt>Failing since</dt>
              <dd>{e.current === 'Fail' && e.failingSince
                ? <>{formatDateTime(e.failingSince)} <small className="muted">({since(e.failingSince)}, run <Link to={`/runs/${e.failingSinceRunId}`}>#{e.failingSinceRunId}</Link>)</small></>
                : <span className="muted">not failing</span>}</dd>
              <dt>Last passed</dt><dd>{e.lastPassedAt ? formatDateTime(e.lastPassedAt) : <span className="muted">never (in recorded history)</span>}</dd>
              <dt>Flaky?</dt>
              <dd>{e.isFlaky
                ? <span className="pill warning">⚠ Yes — {e.flips} verdict changes without a code change</span>
                : <span className="muted">No ({e.flips} change{e.flips === 1 ? '' : 's'} without a code change in the last {data.rule.window} runs)</span>}</dd>
              <dt>Executions</dt><dd>{e.executions}</dd>
            </dl>
          </div>
        ))}
      </section>

      <section className="card">
        <div className="card-head">
          <h2>Timeline <small className="muted">oldest → newest</small></h2>
          <select value={env} onChange={(e) => setEnv(e.target.value)} aria-label="Environment filter">
            <option value="all">All environments</option>
            {environments.map((n) => <option key={n}>{n}</option>)}
          </select>
        </div>
        {entries.length === 0 ? <Empty>No results recorded yet.</Empty> : (
          <>
            <div className="timeline" role="img" aria-label="Result of each run, oldest to newest">
              {entries.map((e) => (
                <button key={e.resultId} className={`tick ${STATUS_META[e.status].tone} ${e.transition === 'Regression' || e.transition === 'Recovery' ? 'transition' : ''}`}
                  title={`Run #${e.runId} · ${e.environmentName} · ${STATUS_META[e.status].label} · ${formatDateTime(e.at)}${e.transition !== 'None' ? ` · ${e.transition}` : ''}`}
                  onClick={() => openEvidence(e)}>
                  {e.transition === 'Regression' ? '↓' : e.transition === 'Recovery' ? '↑' : ''}
                </button>
              ))}
            </div>
            <p className="muted small">Each square is one run. ↓ = regression (passed before, fails now) · ↑ = recovery (failed before, passes now). Compared per environment.</p>
            <div className="table-wrap">
              <table className="table">
                <thead><tr><th>When</th><th>Run</th><th>Environment</th><th>Result</th><th>Change</th><th>Bugs</th><th>Duration</th><th>Evidence</th></tr></thead>
                <tbody>
                  {[...entries].reverse().map((e) => (
                    <tr key={e.resultId}>
                      <td className="nowrap">{formatDateTime(e.at)}</td>
                      <td><Link to={`/runs/${e.runId}`}>#{e.runId}</Link></td>
                      <td>{e.environmentName}</td>
                      <td><StatusPill status={e.status} />{e.errorMessage && e.status !== 'Interrupted' && <div className="error-line">{e.errorMessage.replace(/^Error:\s*/, '').split('\n')[0]}</div>}</td>
                      <td><TransitionBadge t={e.transition} /></td>
                      <td><BugTags bugs={e.knownBugs} /></td>
                      <td className="muted">{formatDuration(e.durationMs)}</td>
                      <td>{e.evidenceCount ? <button className="link" onClick={() => openEvidence(e)}>Open ({e.evidenceCount})</button> : <span className="muted">—</span>}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
        <p className="muted small">Flakiness rule: {data.rule.description}</p>
      </section>

      {evidence && (
        <Drawer onClose={() => setEvidence(null)} title={<>
          <div className="muted small">Run #{evidence.runId}</div>
          <h2>{evidence.title}</h2>
          <Link className="link" to={`/runs/${evidence.runId}`}>Open the whole run →</Link>
        </>}>
          <EvidencePanel scenario={evidence} />
        </Drawer>
      )}
    </>
  );
}

function TransitionBadge({ t }: { t: HistoryEntry['transition'] }) {
  if (t === 'Regression') return <span className="pill critical" title="Passed in the previous run on this environment, fails now"><i>↓</i>Regression</span>;
  if (t === 'Recovery') return <span className="pill good" title="Failed in the previous run on this environment, passes now"><i>↑</i>Recovery</span>;
  if (t === 'FirstResult') return <span className="muted small">first run</span>;
  return <span className="muted small">—</span>;
}
