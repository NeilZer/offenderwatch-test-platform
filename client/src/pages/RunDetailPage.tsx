import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../api/client';
import type { RunSummary, Scenario } from '../api/types';
import { useCanOperate } from '../auth/AuthContext';
import { EvidencePanel } from '../components/EvidencePanel';
import {
  BugTags, Drawer, Empty, ErrorBox, LiveIndicator, Loading, OperatorButton, PageHeader, RunStatusPill, StatusPill, TotalsBar, runPassRate,
} from '../components/ui';
import { formatDateTime, formatDuration, scopeLabel, tcId } from '../lib/format';
import { useAsync } from '../lib/useAsync';
import { runsHub, useHubEvent, useResync } from '../realtime/hub';

/**
 * TM-02 / TM-03 / TM-08 — one run: live progress (queued → running → result, no refresh needed),
 * stop button, every scenario with duration and failure message, and its evidence.
 */
export function RunDetailPage() {
  const runId = Number(useParams().id);
  const canOperate = useCanOperate();
  const { data, setData, error, loading, reload } = useAsync(() => api.run(runId), [runId]);
  const [output, setOutput] = useState<string[]>([]);
  const [selected, setSelected] = useState<number | null>(null);
  const [filter, setFilter] = useState<'all' | 'problems' | 'known' | 'passed'>('all');
  const [stopping, setStopping] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const loadOutput = useCallback(() => api.runOutput(runId).then(setOutput).catch(() => undefined), [runId]);
  useEffect(() => { void loadOutput(); }, [loadOutput]);
  useEffect(() => runsHub.watchRun(runId), [runId]);

  // Coalesces bursts of "I don't know this scenario yet" into a single reload.
  const reloadPending = useRef(false);
  const reloadOnce = useCallback(async () => {
    if (reloadPending.current) return;
    reloadPending.current = true;
    try { await reload(); } finally { reloadPending.current = false; }
  }, [reload]);

  // Decisions are taken on the CURRENT data, read synchronously from a ref — never from inside a
  // state updater (React may run updaters later, so a variable set there can still be stale).
  const dataRef = useRef(data);
  dataRef.current = data;

  // Live updates — merged into the loaded snapshot.
  useHubEvent<[RunSummary]>('runUpdated', (run) => {
    if (run.id !== runId) return;
    const current = dataRef.current;
    setData((d) => (d ? { ...d, run } : d));
    // The suite has just announced its scenarios but this page does not have them yet.
    if (current && run.totals.total > current.scenarios.length) void reloadOnce();
    if (run.finishedAt) setStopping(false);
  }, [runId, reloadOnce]);
  useHubEvent<[Scenario]>('scenarioUpdated', (scenario) => {
    if (scenario.runId !== runId) return;
    const current = dataRef.current;
    if (current && !current.scenarios.some((s) => s.id === scenario.id)) {
      // The page loaded before the suite announced its scenarios: fetch the full list once.
      void reloadOnce();
      return;
    }
    setData((d) => d && { ...d, scenarios: d.scenarios.map((s) => (s.id === scenario.id ? scenario : s)) });
  }, [runId, reloadOnce]);
  useHubEvent<[number, string[]]>('runOutput', (id, lines) => {
    if (id === runId) setOutput((o) => [...o, ...lines].slice(-3000));
  }, [runId]);
  // After a reconnect, reload everything from the API: nothing missed while offline is lost.
  useResync(() => { void reload(); void loadOutput(); }, [reload, loadOutput]);

  const scenarios = data?.scenarios ?? [];
  const visible = useMemo(() => scenarios.filter((s) => {
    if (filter === 'problems') return s.status === 'Failed' || s.status === 'UnexpectedPass';
    if (filter === 'known') return s.status === 'ExpectedFailure';
    if (filter === 'passed') return s.status === 'Passed';
    return true;
  }), [scenarios, filter]);
  const groups = useMemo(() => groupByFile(visible), [visible]);
  const selectedScenario = scenarios.find((s) => s.id === selected) ?? null;

  if (loading && !data) return <Loading />;
  if (error && !data) return <ErrorBox message={error} onRetry={reload} />;
  if (!data) return null;
  const run = data.run;
  const active = !run.finishedAt;

  async function stop() {
    setStopping(true);
    setActionError(null);
    try { await api.stopRun(runId); } catch (e) { setActionError((e as Error).message); setStopping(false); }
  }

  return (
    <>
      <PageHeader
        title={`Run #${run.id}`}
        subtitle={<>{run.environmentName} · <code>{run.environmentBaseUrl}</code></>}
        actions={<>
          {active && <LiveIndicator />}
          {active && (
            <OperatorButton canOperate={canOperate} className="btn danger" onClick={stop} disabled={stopping}>
              {stopping ? 'Stopping…' : '■ Stop run'}
            </OperatorButton>
          )}
        </>}
      />
      <ErrorBox message={actionError} />

      <section className="card run-head">
        <div className="run-facts">
          <Fact label="Status"><RunStatusPill run={run} />{run.statusReason && <small className="muted"> {run.statusReason}</small>}</Fact>
          <Fact label="Trigger">{run.trigger}{run.startedBy ? ` · ${run.startedBy}` : ''}</Fact>
          <Fact label="Scope">{scopeLabel(run)}</Fact>
          <Fact label="Started">{formatDateTime(run.startedAt ?? run.createdAt)}</Fact>
          <Fact label="Ended">{formatDateTime(run.finishedAt)}</Fact>
          <Fact label="Duration">{formatDuration(run.durationMs)}</Fact>
          <Fact label="Pass rate">{run.completed ? `${runPassRate(run)}%` : '—'}</Fact>
        </div>
        <div className="progress-label">
          <b>{run.completed}</b> of <b>{run.totals.total || '…'}</b> scenarios finished
        </div>
        <TotalsBar run={run} />
      </section>

      <section className="card">
        <div className="card-head">
          <h2>Scenarios</h2>
          <div className="segmented" role="group" aria-label="Filter scenarios">
            {([['all', 'All'], ['problems', 'Needs attention'], ['known', 'Known bugs'], ['passed', 'Passed']] as const).map(([v, l]) => (
              <button key={v} className={filter === v ? 'active' : ''} onClick={() => setFilter(v)}>{l}</button>
            ))}
          </div>
        </div>
        {scenarios.length === 0 ? (
          <Empty>{active ? 'Waiting for the suite to announce its scenarios…' : 'This run reported no scenarios. See the runner output below.'}</Empty>
        ) : visible.length === 0 ? (
          <Empty>No scenario matches this filter.</Empty>
        ) : (
          groups.map(([file, items]) => (
            <div key={file} className="group">
              <h3 className="group-title">{file}</h3>
              <table className="table scenarios">
                <tbody>
                  {items.map((s) => (
                    <tr key={s.id} className={`clickable row-${s.status}`} onClick={() => setSelected(s.id)}>
                      <td className="col-status"><StatusPill status={s.status} /></td>
                      <td className="col-tc mono">{tcId(s.tags)}</td>
                      <td>
                        <div className="title-line">{s.title.replace(/^TC-\d+\s*/, '')}</div>
                        {s.errorMessage && s.status !== 'Interrupted' && <div className="error-line">{firstLine(s.errorMessage)}</div>}
                      </td>
                      <td><BugTags bugs={s.knownBugs} /></td>
                      <td className="col-num muted">{formatDuration(s.durationMs)}</td>
                      <td className="col-num muted">{s.evidence.length > 0 && (
                        <span title={`${s.evidence.length} evidence files saved: ${[...new Set(s.evidence.map((e) => e.kind))].join(', ')} — click the row to open them`}>📎 {s.evidence.length}</span>
                      )}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ))
        )}
      </section>

      <RunnerOutput lines={output} live={active} />

      {selectedScenario && (
        <Drawer onClose={() => setSelected(null)} title={<>
          <div className="muted small">Run #{run.id} · {run.environmentName}</div>
          <h2>{selectedScenario.title}</h2>
          <Link className="link" to={`/tests/${selectedScenario.testCaseId}`}>View this test's history →</Link>
        </>}>
          <EvidencePanel scenario={selectedScenario} />
        </Drawer>
      )}
    </>
  );
}

function Fact({ label, children }: { label: string; children: React.ReactNode }) {
  return <div className="fact"><span className="fact-label">{label}</span><span className="fact-value">{children}</span></div>;
}

function RunnerOutput({ lines, live }: { lines: string[]; live: boolean }) {
  const ref = useRef<HTMLPreElement>(null);
  const [open, setOpen] = useState(false);
  useEffect(() => {
    if (open && ref.current) ref.current.scrollTop = ref.current.scrollHeight;
  }, [lines, open]);
  return (
    <section className="card">
      <details open={open} onToggle={(e) => setOpen((e.target as HTMLDetailsElement).open)}>
        <summary><h2>Runner output {live && <small className="muted">(streaming)</small>}</h2><span className="muted">{lines.length} lines</span></summary>
        <pre ref={ref} className="code console">{lines.length ? lines.join('\n') : 'No output yet.'}</pre>
      </details>
    </section>
  );
}

function groupByFile(scenarios: Scenario[]): [string, Scenario[]][] {
  const map = new Map<string, Scenario[]>();
  for (const s of scenarios) {
    const key = s.file || s.project;
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(s);
  }
  return [...map.entries()];
}

const firstLine = (text: string) => text.replace(/^Error:\s*/, '').split('\n')[0];
