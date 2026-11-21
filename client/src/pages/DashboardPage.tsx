import { useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import type { FailingTest, RunSummary, VerdictBasis } from '../api/types';
import { useCanOperate } from '../auth/AuthContext';
import { StartRunDialog } from '../components/StartRunDialog';
import { TrendChart } from '../components/TrendChart';
import { BugTags, Empty, ErrorBox, Loading, OperatorButton, PageHeader, RunStatusPill, StatusPill, TotalsBar, runPassRate } from '../components/ui';
import { formatDateTime, formatDuration, scopeLabel, since, tcId } from '../lib/format';
import { useAsync } from '../lib/useAsync';
import { useHubEvent, useResync } from '../realtime/hub';

/**
 * TM-07 — landing page; replaces the static Part 4 dashboard as the source of the go / no-go verdict.
 * Latest run per environment · pass-rate trend · currently failing tests and for how long.
 */
export function DashboardPage() {
  const canOperate = useCanOperate();
  const { data, error, loading, reload } = useAsync(() => api.dashboard(), []);
  const [starting, setStarting] = useState(false);
  useHubEvent<[RunSummary]>('runUpdated', (run) => { if (run.finishedAt) void reload(); }, [reload]);
  useResync(() => void reload(), [reload]);

  if (loading && !data) return <Loading />;
  if (error && !data) return <ErrorBox message={error} onRetry={reload} />;
  if (!data) return null;
  const unexpected = data.currentlyFailing.filter((f) => !f.expected);
  const known = data.currentlyFailing.filter((f) => f.expected);

  return (
    <>
      <PageHeader title="Dashboard" subtitle="Testing status at a glance — always from the latest recorded runs."
        actions={<OperatorButton canOperate={canOperate} className="btn primary" onClick={() => setStarting(true)}>▶ Start run</OperatorButton>} />

      <section className={`verdict ${data.verdict.go ? 'go' : 'nogo'}`} aria-label="Release recommendation">
        <span className="verdict-badge">{data.verdict.go ? '✓ GO' : '✕ NO-GO'}</span>
        <p>{data.verdict.line.replace(/^(NO-GO|GO) — /, '')}</p>
        {data.verdict.basis && <VerdictBasisLine basis={data.verdict.basis} env={data.verdict.environmentName} />}
      </section>

      {data.activeRuns.length > 0 && (
        <section className="card active-runs">
          <h2><span className="spinner" /> Running now</h2>
          {data.activeRuns.map((r) => (
            <Link key={r.id} to={`/runs/${r.id}`} className="active-run">
              <b>Run #{r.id}</b> on {r.environmentName} — {r.completed}/{r.totals.total || '…'} finished · watch live →
            </Link>
          ))}
        </section>
      )}

      <h2 className="section-title">Latest complete run per environment</h2>
      {data.latestRunPerEnvironment.length === 0 ? <Empty>No runs yet.</Empty> : (
        <div className="grid-cards">
          {data.latestRunPerEnvironment.map(({ run: r, isComplete, newerRunIds }) => (
            <Link key={r.id} to={`/runs/${r.id}`} className="card env-card">
              <div className="env-card-head">
                <h3>{r.environmentName}</h3>
                <RunStatusPill run={r} />
              </div>
              <p className="hero">{r.completed ? runPassRate(r) : '—'}<span>%</span></p>
              <p className="muted">pass rate · run #{r.id} · {formatDateTime(r.finishedAt ?? r.startedAt)} · {formatDuration(r.durationMs)}</p>
              {!isComplete && <p className="small"><span className="pill neutral">No complete run yet — showing partial run: {scopeLabel(r)}</span></p>}
              {newerRunIds.length > 0 && (
                <p className="small muted">Newer partial run{newerRunIds.length > 1 ? 's' : ''}: {newerRunIds.map((id) => `#${id}`).join(', ')} — included in the verdict for the tests they ran.</p>
              )}
              <TotalsBar run={r} />
            </Link>
          ))}
        </div>
      )}

      <section className="card">
        <h2>Pass-rate trend</h2>
        <p className="muted small">Share of executed scenarios whose requirement works (passed ÷ executed), for every completed run. Known-bug failures count as failures. Partial runs (API only, UI only, filtered, incomplete) are drawn as hollow points: their pass rate covers only part of the suite.</p>
        <TrendChart points={data.trend} />
      </section>

      <section className="card">
        <h2>Currently failing <span className="count">{failingSummary(data.currentlyFailing)}</span></h2>
        {data.currentlyFailing.length === 0 ? <Empty>Nothing is failing in the latest runs. 🎉</Empty> : (
          <>
            {unexpected.length > 0 && <FailingTable title="Needs attention — failing unexpectedly" rows={unexpected} />}
            {unexpected.length === 0 && <p className="notice">✓ No unexpected failures — every failing test is explained by a documented bug.</p>}
            {known.length > 0 && (
              <details className="known-bugs">
                <summary>
                  <span>{failingSummary(known)} — failing on <b>{new Set(known.flatMap((f) => f.knownBugs.map((b) => b.id))).size}</b> documented bugs (expected) — show</span>
                </summary>
                <FailingTable title="Failing on documented bugs (expected)" rows={known} />
              </details>
            )}
          </>
        )}
      </section>

      {data.recentNotifications.length > 0 && (
        <section className="card">
          <h2>Regression notifications</h2>
          <ul className="plain">
            {data.recentNotifications.map((n) => (
              <li key={n.id}>🔔 <Link to={`/runs/${n.runId}`}>{n.summary}</Link> <small className="muted">{formatDateTime(n.createdAt)} · {n.delivered ? 'sent to webhook' : n.target ? `webhook failed: ${n.deliveryError}` : 'no webhook configured (logged only)'}</small></li>
            ))}
          </ul>
        </section>
      )}
      {starting && <StartRunDialog onClose={() => setStarting(false)} />}
    </>
  );
}

/** "45 tests on Neil (personal) · 45 on Shared (base)" — the same test failing on two environments is not two tests. */
function failingSummary(rows: FailingTest[]): string {
  const perEnv = new Map<string, number>();
  rows.forEach((r) => perEnv.set(r.environmentName, (perEnv.get(r.environmentName) ?? 0) + 1));
  return [...perEnv.entries()].map(([env, n], i) => (i === 0 ? `${n} tests on ${env}` : `${n} on ${env}`)).join(' · ') || '0';
}

/** Plain-language explanation of what the verdict is built from, and how it compares with the last complete run. */
function VerdictBasisLine({ basis, env }: { basis: VerdictBasis; env: string | null }) {
  const runs = basis.sourceRunIds;
  const runText = runs.length === 1 ? <>run <Link to={`/runs/${runs[0]}`}>#{runs[0]}</Link></> : <>runs {runs.map((id, i) => (
    <span key={id}>{i > 0 && (i === runs.length - 1 ? ' and ' : ', ')}<Link to={`/runs/${id}`}>#{id}</Link></span>
  ))}</>;
  return (
    <div className="verdict-basis">
      <p>
        Based on the <b>latest result of each of the {basis.testsConsidered} tests</b> on {env ?? 'the default environment'}, taken from {runText}.
        A newer run — even a partial one — updates the tests it ran; tests it did not run keep their last result.
        {basis.testsNeverRun > 0 && <> <b>{basis.testsNeverRun}</b> test(s) have no result yet.</>}
      </p>
      {basis.baselineRunId && (
        <p>
          Compared with the last complete run <Link to={`/runs/${basis.baselineRunId}`}>#{basis.baselineRunId}</Link>:{' '}
          {basis.improvedSinceBaseline === 0 && basis.worsenedSinceBaseline === 0
            ? 'no test changed since then.'
            : <><b className="t good">{basis.improvedSinceBaseline} now pass</b>, <b className="t critical">{basis.worsenedSinceBaseline} now fail</b>.</>}
          {' '}<Link to={`/compare?base=${basis.baselineRunId}&target=${runs[runs.length - 1]}`}>Compare runs →</Link>
        </p>
      )}
    </div>
  );
}

function FailingTable({ title, rows }: { title: string; rows: FailingTest[] }) {
  return (
    <>
      <h3>{title} <span className="count">{rows.length}</span></h3>
      <div className="table-wrap">
        <table className="table">
          <thead><tr><th>TC</th><th>Test</th><th>Environment</th><th>State</th><th>Failing for</th><th>Since</th><th>Last passed</th><th>Bugs</th></tr></thead>
          <tbody>
            {rows.map((f) => (
              <tr key={`${f.testCaseId}-${f.environmentName}`}>
                <td className="mono">{tcId(f.tags)}</td>
                <td><Link to={`/tests/${f.testCaseId}`}>{f.title.replace(/^TC-\d+\s*/, '')}</Link>{f.isFlaky && <span className="pill warning"> ⚠ Flaky</span>}</td>
                <td>{f.environmentName}</td>
                <td><StatusPill status={f.status} /></td>
                <td className="nowrap"><b>{since(f.failingSince)}</b></td>
                <td className="nowrap">{formatDateTime(f.failingSince)}{f.failingSinceRunId && <> · <Link to={`/runs/${f.failingSinceRunId}`}>#{f.failingSinceRunId}</Link></>}</td>
                <td className="nowrap">{f.lastPassedAt ? formatDateTime(f.lastPassedAt) : <span className="muted">never</span>}</td>
                <td><BugTags bugs={f.knownBugs} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
