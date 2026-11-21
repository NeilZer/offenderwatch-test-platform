import { FormEvent, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import { useCanOperate } from '../auth/AuthContext';
import { Empty, ErrorBox, OperatorButton, PageHeader } from '../components/ui';
import { formatDateTime } from '../lib/format';
import { useAsync } from '../lib/useAsync';

/** B-03 — regression notifications: webhook (Slack / Teams) and the log of every notification. */
export function SettingsPage() {
  const canOperate = useCanOperate();
  const [webhook, setWebhook] = useState('');
  const [saved, setSaved] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const notifications = useAsync(() => api.notifications(), []);

  useEffect(() => { api.settings().then((s) => setWebhook(s.webhookUrl ?? '')).catch((e) => setError(e.message)); }, []);

  async function save(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSaved(null);
    try {
      const s = await api.saveSettings(webhook.trim() || null);
      setSaved(s.webhookUrl ? 'Saved — new regressions will be posted to this webhook.' : 'Saved — webhook removed; notifications are only logged.');
    } catch (err) { setError((err as Error).message); }
  }

  return (
    <>
      <PageHeader title="Notifications" subtitle="When a test that passed in the previous run on an environment now FAILS UNEXPECTEDLY, the platform notifies. Known-bug failures never notify." />
      <section className="card">
        <h2>Webhook</h2>
        <form className="form inline" onSubmit={save}>
          <label>Slack or Teams incoming-webhook URL
            <input type="url" value={webhook} onChange={(e) => setWebhook(e.target.value)} placeholder="https://hooks.slack.com/services/…" disabled={!canOperate} />
          </label>
          <OperatorButton canOperate={canOperate} className="btn primary" type="submit">Save</OperatorButton>
        </form>
        <p className="muted small">The message is posted as <code>{'{"text": "…"}'}</code>, which both Slack and Teams accept. Without a webhook every notification is still logged below.</p>
        {saved && <p className="notice">{saved}</p>}
        <ErrorBox message={error} />
      </section>
      <section className="card">
        <h2>Notification log</h2>
        <ErrorBox message={notifications.error} />
        {!notifications.data?.length ? <Empty>No regression has been detected yet.</Empty> : (
          <table className="table">
            <thead><tr><th>When</th><th>Run</th><th>Summary</th><th>Delivery</th></tr></thead>
            <tbody>{notifications.data.map((n) => (
              <tr key={n.id}>
                <td className="nowrap">{formatDateTime(n.createdAt)}</td>
                <td><Link to={`/runs/${n.runId}`}>#{n.runId}</Link></td>
                <td>{n.summary}</td>
                <td>{n.delivered ? <span className="pill good">Sent</span> : n.target ? <span className="pill critical">Failed: {n.deliveryError}</span> : <span className="pill neutral">Logged only</span>}</td>
              </tr>
            ))}</tbody>
          </table>
        )}
      </section>
    </>
  );
}
