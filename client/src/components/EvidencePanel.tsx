import { Fragment, useEffect, useMemo, useState } from 'react';
import type { Evidence, Scenario } from '../api/types';
import { formatBytes, formatDateTime, formatDuration } from '../lib/format';
import { BugTags, StatusPill } from './ui';

/**
 * TM-08 — everything captured for one scenario result, from the run view and the history view.
 * Tabs appear only when that evidence exists: Summary · Screenshot · Log · API · Error context · Files.
 */
type Tab = 'summary' | 'screenshot' | 'log' | 'api' | 'context' | 'files';

export function EvidencePanel({ scenario }: { scenario: Scenario }) {
  const byKind = useMemo(() => groupByKind(scenario.evidence), [scenario.evidence]);
  const failed = ['Failed', 'ExpectedFailure', 'UnexpectedPass'].includes(scenario.status);
  const tabs: { id: Tab; label: string; show: boolean }[] = [
    { id: 'summary', label: 'Summary', show: true },
    { id: 'screenshot', label: 'Screenshot', show: !!byKind.Screenshot?.length },
    { id: 'log', label: 'Log', show: !!byKind.Log?.length },
    { id: 'api', label: 'API request / response', show: !!byKind.ApiExchange?.length },
    { id: 'context', label: 'Error context', show: !!byKind.ErrorContext?.length },
    { id: 'files', label: `All files (${scenario.evidence.length})`, show: scenario.evidence.length > 0 },
  ];
  const [tab, setTab] = useState<Tab>(failed && byKind.Screenshot?.length ? 'screenshot' : 'summary');
  useEffect(() => setTab(failed && byKind.Screenshot?.length ? 'screenshot' : 'summary'), [scenario.id]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div className="evidence">
      <div className="evidence-meta">
        <StatusPill status={scenario.status} />
        <BugTags bugs={scenario.knownBugs} />
        <span className="muted">{scenario.project.toUpperCase()} · {formatDuration(scenario.durationMs)} · finished {formatDateTime(scenario.finishedAt)}</span>
      </div>
      <nav className="tabs" role="tablist">
        {tabs.filter((t) => t.show).map((t) => (
          <button key={t.id} role="tab" aria-selected={tab === t.id} className={tab === t.id ? 'active' : ''} onClick={() => setTab(t.id)}>{t.label}</button>
        ))}
      </nav>
      <div className="tab-body">
        {tab === 'summary' && <Summary scenario={scenario} />}
        {tab === 'screenshot' && byKind.Screenshot?.map((e) => (
          <a key={e.id} href={e.url} target="_blank" rel="noreferrer" title="Open full size">
            <img className="shot" src={e.url} alt={`Screenshot ${e.fileName}`} />
          </a>
        ))}
        {tab === 'log' && <TextEvidence evidence={byKind.Log![0]} />}
        {tab === 'api' && <ApiExchanges evidence={byKind.ApiExchange![0]} failed={failed} />}
        {tab === 'context' && <TextEvidence evidence={byKind.ErrorContext![0]} />}
        {tab === 'files' && <FileList evidence={scenario.evidence} />}
      </div>
    </div>
  );
}

function Summary({ scenario }: { scenario: Scenario }) {
  return (
    <div className="summary">
      <dl className="kv">
        <dt>Test</dt><dd>{scenario.title}</dd>
        <dt>File</dt><dd><code>{scenario.file}</code></dd>
        <dt>Tags</dt><dd>{scenario.tags || '—'}</dd>
        {scenario.knownBugs.map((b) => (
          <Fragment key={b.id}><dt>Known bug</dt><dd><b>{b.id}</b> {b.title} {b.severity && <span className="muted">({b.severity})</span>}</dd></Fragment>
        ))}
      </dl>
      {scenario.errorMessage ? (
        <>
          <h3>Failure message</h3>
          <pre className="code error">{scenario.errorMessage}</pre>
          {scenario.errorStack && (
            <details>
              <summary>Stack trace</summary>
              <pre className="code">{scenario.errorStack}</pre>
            </details>
          )}
        </>
      ) : (
        <p className="muted">No failure recorded for this scenario.</p>
      )}
    </div>
  );
}

function useText(url: string) {
  const [text, setText] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    let alive = true;
    setText(null);
    fetch(url, { credentials: 'same-origin' })
      .then((r) => (r.ok ? r.text() : Promise.reject(new Error(`HTTP ${r.status}`))))
      .then((t) => alive && setText(t))
      .catch((e) => alive && setError(e.message));
    return () => { alive = false; };
  }, [url]);
  return { text, error };
}

function TextEvidence({ evidence }: { evidence: Evidence }) {
  const { text, error } = useText(evidence.url);
  if (error) return <p className="error-box">Could not load {evidence.fileName}: {error}</p>;
  if (text === null) return <p className="muted">Loading…</p>;
  return <pre className="code log">{text}</pre>;
}

interface Exchange { phase?: 'test' | 'teardown'; at: string; method: string; url: string; requestBody: unknown; status: number; responseBody: string }

function ApiExchanges({ evidence, failed }: { evidence: Evidence; failed: boolean }) {
  const { text, error } = useText(evidence.url);
  const [showAll, setShowAll] = useState(false);
  if (error) return <p className="error-box">Could not load API evidence: {error}</p>;
  if (text === null) return <p className="muted">Loading…</p>;
  let exchanges: Exchange[];
  try { exchanges = JSON.parse(text); } catch { return <pre className="code">{text}</pre>; }
  // The offending / final call is the scenario's own last call — never its clean-up (teardown).
  const testCalls = exchanges.filter((x) => x.phase !== 'teardown');
  const last = testCalls[testCalls.length - 1] ?? exchanges[exchanges.length - 1];
  const shown = showAll ? exchanges : [last];
  return (
    <div>
      <p className="muted">
        {failed ? 'The last call before the scenario ended — the offending request/response.' : 'The final request/response of the scenario.'}{' '}
        {exchanges.length > 1 && <button className="link" onClick={() => setShowAll(!showAll)}>{showAll ? 'Show only the last' : `Show all ${exchanges.length} calls`}</button>}
      </p>
      {shown.map((x, i) => (
        <div key={i} className="exchange">
          <div className="exchange-head"><b>{x.method}</b> <code>{x.url}</code> <span className={`status-code s${String(x.status)[0]}`}>{x.status}</span>
            {x.phase === 'teardown' && <span className="pill neutral">clean-up</span>}</div>
          {x.requestBody !== null && x.requestBody !== undefined && (
            <><h4>Request body</h4><pre className="code">{pretty(x.requestBody)}</pre></>
          )}
          <h4>Response body</h4>
          <pre className="code">{x.responseBody ? pretty(x.responseBody) : '<empty body>'}</pre>
        </div>
      ))}
    </div>
  );
}

function FileList({ evidence }: { evidence: Evidence[] }) {
  return (
    <table className="table compact">
      <thead><tr><th>Kind</th><th>File</th><th>Size</th><th>SHA-256 (integrity)</th><th /></tr></thead>
      <tbody>
        {evidence.map((e) => (
          <tr key={e.id}>
            <td>{e.kind}</td><td>{e.fileName}</td><td>{formatBytes(e.sizeBytes)}</td>
            <td><code title={e.sha256}>{e.sha256.slice(0, 16)}…</code></td>
            <td><a className="link" href={`${e.url}?download=true`}>Download</a></td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function groupByKind(evidence: Evidence[]): Partial<Record<string, Evidence[]>> {
  return evidence.reduce<Partial<Record<string, Evidence[]>>>((acc, e) => {
    (acc[e.kind] ??= []).push(e);
    return acc;
  }, {});
}

function pretty(value: unknown): string {
  if (typeof value !== 'string') return JSON.stringify(value, null, 2);
  try { return JSON.stringify(JSON.parse(value), null, 2); } catch { return value; }
}
