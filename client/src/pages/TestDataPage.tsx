import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import type { CleanupReport, Environment } from '../api/types';
import { useCanOperate } from '../auth/AuthContext';
import { Empty, ErrorBox, Loading, Modal, OperatorButton, PageHeader } from '../components/ui';
import { formatDateTime } from '../lib/format';
import { useAsync } from '../lib/useAsync';
import { useHubEvent } from '../realtime/hub';

/**
 * TM-06 — data the scenarios created, attributed to the run that created it, and the
 * "Clean test data" action. Seeded data (protected ids) is never deleted.
 */
const STATE_LABEL: Record<string, { label: string; tone: string }> = {
  Active: { label: 'Still in the application', tone: 'serious' },
  DeletedByTest: { label: 'Deleted by its test', tone: 'good' },
  CleanedByPlatform: { label: 'Cleaned by platform', tone: 'good' },
  AlreadyGone: { label: 'Already gone', tone: 'neutral' },
  Protected: { label: 'Protected (seeded)', tone: 'info' },
  CleanupFailed: { label: 'Clean-up failed — retried next time', tone: 'critical' },
  RemovedWithParent: { label: 'Removed with its offender (verified)', tone: 'good' },
  OrphanedInApplication: { label: 'Left in the app — BUG-02', tone: 'serious' },
};

export function TestDataPage() {
  const canOperate = useCanOperate();
  const [environments, setEnvironments] = useState<Environment[]>([]);
  const [environmentId, setEnvironmentId] = useState<number | null>(null);
  const [activeOnly, setActiveOnly] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [report, setReport] = useState<CleanupReport | null>(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  useEffect(() => {
    api.environments().then((envs) => {
      setEnvironments(envs);
      setEnvironmentId((envs.find((e) => e.isDefault) ?? envs[0])?.id ?? null);
    }).catch((e) => setActionError(e.message));
  }, []);

  const records = useAsync(() => (environmentId ? api.testData(environmentId, activeOnly) : Promise.resolve([])), [environmentId, activeOnly]);
  useHubEvent('testDataChanged', () => void records.reload(), [records.reload]);

  const byRun = useMemo(() => {
    const map = new Map<number, NonNullable<typeof records.data>>();
    for (const r of records.data ?? []) {
      if (!map.has(r.runId)) map.set(r.runId, []);
      map.get(r.runId)!.push(r);
    }
    return [...map.entries()];
  }, [records.data]);
  // Still in the application: never cleaned, or a previous clean-up attempt failed (it is retried).
  const waiting = (state: string) => state === 'Active' || state === 'CleanupFailed';
  const activeCount = (records.data ?? []).filter((r) => waiting(r.state)).length;
  const orphanCount = (records.data ?? []).filter((r) => r.state === 'OrphanedInApplication').length;
  const env = environments.find((e) => e.id === environmentId);

  async function clean() {
    if (!environmentId) return;
    setBusy(true);
    setActionError(null);
    try {
      setReport(await api.cleanTestData(environmentId));
      setConfirming(false);
      await records.reload();
    } catch (e) {
      setActionError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <PageHeader title="Test data" subtitle="Records the scenarios created in the application, grouped by the run that created them."
        actions={<OperatorButton canOperate={canOperate} className="btn danger" onClick={() => setConfirming(true)} disabled={!environmentId}>🧹 Clean test data</OperatorButton>} />
      <div className="toolbar">
        <select value={environmentId ?? ''} onChange={(e) => setEnvironmentId(Number(e.target.value))} aria-label="Environment">
          {environments.map((e) => <option key={e.id} value={e.id}>{e.name}</option>)}
        </select>
        <label className="check"><input type="checkbox" checked={activeOnly} onChange={(e) => setActiveOnly(e.target.checked)} /> Only data still in the application</label>
        <span className={`pill ${activeCount ? 'serious' : 'good'}`}>{activeCount} record(s) waiting for clean-up</span>
        {orphanCount > 0 && (
          <span className="pill neutral" title="The application keeps a deleted offender's points and has no endpoint to delete a single point (BUG-02). Verified against the application.">
            {orphanCount} point(s) left in the app (BUG-02)
          </span>
        )}
      </div>
      {env && <p className="muted small">Protected seeded ids on {env.name}: <b>{env.protectedIds}</b> — never deleted, even if a run registers them.</p>}
      <ErrorBox message={actionError ?? records.error} />
      {report && (
        <div className="card notice">
          <b>Clean-up finished.</b> {report.details[0]}
          {report.details.slice(1).map((d) => <div key={d} className="muted small">{d}</div>)}
          <button className="link" onClick={() => setReport(null)}>dismiss</button>
        </div>
      )}
      {records.loading && !records.data ? <Loading /> : byRun.length === 0 ? <Empty>No test data recorded for this environment.</Empty> : byRun.map(([runId, items]) => (
        <section key={runId} className="card">
          <h2><Link to={`/runs/${runId}`}>Run #{runId}</Link> <span className="count">{items.length}</span></h2>
          <div className="table-wrap">
            <table className="table compact">
              <thead><tr><th>Kind</th><th>Id in app</th><th>Record</th><th>National ID</th><th>Created by scenario</th><th>Created</th><th>State</th><th>Note</th></tr></thead>
              <tbody>{items.map((r) => (
                <tr key={r.id}>
                  <td>{r.kind === 'LocationPoint' ? 'Location point' : 'Offender'}</td>
                  <td className="mono">{r.externalId}{r.parentExternalId && <small className="muted"> (of {r.parentExternalId})</small>}</td>
                  <td>{r.label}</td>
                  <td className="mono">{r.naturalKey ?? '—'}</td>
                  <td className="small">{r.scenarioTitle ?? '—'}</td>
                  <td className="nowrap">{formatDateTime(r.createdAt)}</td>
                  <td><span className={`pill ${STATE_LABEL[r.state]?.tone ?? 'neutral'}`}>{STATE_LABEL[r.state]?.label ?? r.state}</span></td>
                  <td className="small muted">{r.note}</td>
                </tr>
              ))}</tbody>
            </table>
          </div>
        </section>
      ))}
      {confirming && env && (
        <Modal title="Clean test data?" onClose={() => setConfirming(false)}>
          <p>This deletes the <b>{activeCount}</b> offender record(s) still left by test runs on <b>{env.name}</b>, through the application's own API.</p>
          <ul className="plain small">
            <li>✓ Only records registered by platform runs are touched.</li>
            <li>✓ Seeded ids <b>{env.protectedIds}</b> are never deleted.</li>
            <li>✓ Each record is re-checked first: if its national ID no longer matches, it is left alone.</li>
          </ul>
          <div className="form-actions">
            <button className="btn" onClick={() => setConfirming(false)}>Cancel</button>
            <button className="btn danger" onClick={clean} disabled={busy}>{busy ? 'Cleaning…' : 'Clean now'}</button>
          </div>
        </Modal>
      )}
    </>
  );
}
