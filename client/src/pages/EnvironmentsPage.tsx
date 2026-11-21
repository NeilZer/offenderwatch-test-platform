import { FormEvent, useState } from 'react';
import { api, EnvironmentInput } from '../api/client';
import type { Environment } from '../api/types';
import { useCanOperate } from '../auth/AuthContext';
import { Empty, ErrorBox, Loading, Modal, OperatorButton, PageHeader } from '../components/ui';
import { formatDateTime } from '../lib/format';
import { useAsync } from '../lib/useAsync';

/** TM-01 — target environments: create, edit, delete, mark one as default. No URL is hard-coded anywhere. */
export function EnvironmentsPage() {
  const canOperate = useCanOperate();
  const { data, error, loading, reload } = useAsync(() => api.environments(), []);
  const [editing, setEditing] = useState<Environment | 'new' | null>(null);
  const [deleting, setDeleting] = useState<Environment | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  async function act(action: () => Promise<unknown>) {
    setActionError(null);
    try { await action(); await reload(); } catch (e) { setActionError((e as Error).message); }
  }

  return (
    <>
      <PageHeader title="Environments" subtitle="Where the suite runs. A run records the environment it used, so history stays correct even after edits."
        actions={<OperatorButton canOperate={canOperate} className="btn primary" onClick={() => setEditing('new')}>+ Add environment</OperatorButton>} />
      <ErrorBox message={actionError ?? error} onRetry={error ? reload : undefined} />
      {loading && !data ? <Loading /> : !data?.length ? <Empty>No environments yet. Add one to start running tests.</Empty> : (
        <div className="card table-wrap">
          <table className="table">
            <thead><tr><th>Name</th><th>Base URL</th><th>Protected seeded ids</th><th>Updated</th><th /></tr></thead>
            <tbody>{data.map((env) => (
              <tr key={env.id}>
                <td><b>{env.name}</b> {env.isDefault && <span className="pill good">★ Default</span>}</td>
                <td><code>{env.baseUrl}</code></td>
                <td className="mono">{env.protectedIds}</td>
                <td className="nowrap">{formatDateTime(env.updatedAt)}</td>
                <td className="row-actions">
                  {!env.isDefault && <OperatorButton canOperate={canOperate} className="btn small" onClick={() => act(() => api.makeDefaultEnvironment(env.id))}>Make default</OperatorButton>}
                  <OperatorButton canOperate={canOperate} className="btn small" onClick={() => setEditing(env)}>Edit</OperatorButton>
                  <OperatorButton canOperate={canOperate} className="btn small danger" onClick={() => setDeleting(env)}>Delete</OperatorButton>
                </td>
              </tr>
            ))}</tbody>
          </table>
        </div>
      )}
      {editing && <EnvironmentForm env={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={reload} />}
      {deleting && (
        <Modal title={`Delete ${deleting.name}?`} onClose={() => setDeleting(null)}>
          <p>The environment disappears from the list. <b>Its run history is kept</b> — every run stored the environment name and URL it used.</p>
          <div className="form-actions">
            <button className="btn" onClick={() => setDeleting(null)}>Cancel</button>
            <button className="btn danger" onClick={() => { const id = deleting.id; setDeleting(null); void act(() => api.deleteEnvironment(id)); }}>Delete</button>
          </div>
        </Modal>
      )}
    </>
  );
}

function EnvironmentForm({ env, onClose, onSaved }: { env: Environment | null; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState<EnvironmentInput>({
    name: env?.name ?? '', baseUrl: env?.baseUrl ?? '', isDefault: env?.isDefault ?? false, protectedIds: env?.protectedIds ?? '1-11',
  });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      if (env) await api.updateEnvironment(env.id, form);
      else await api.createEnvironment(form);
      onSaved();
      onClose();
    } catch (err) {
      setError((err as Error).message);
      setBusy(false);
    }
  }

  return (
    <Modal title={env ? `Edit ${env.name}` : 'Add environment'} onClose={onClose}>
      <form className="form" onSubmit={submit}>
        <label>Name<input required maxLength={100} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="e.g. Staging" /></label>
        <label>Base URL<input required type="url" value={form.baseUrl} onChange={(e) => setForm({ ...form, baseUrl: e.target.value })} placeholder="https://host/AQApplication/YourName/" /></label>
        <label>Protected seeded ids
          <input value={form.protectedIds} onChange={(e) => setForm({ ...form, protectedIds: e.target.value })} placeholder="1-11" />
          <small className="muted">Ids "Clean test data" must never delete, e.g. <code>1-11</code> or <code>1-11,15</code>.</small>
        </label>
        <label className="check"><input type="checkbox" checked={form.isDefault} onChange={(e) => setForm({ ...form, isDefault: e.target.checked })} /> Default environment</label>
        <ErrorBox message={error} />
        <div className="form-actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn primary" disabled={busy}>{busy ? 'Saving…' : 'Save'}</button>
        </div>
      </form>
    </Modal>
  );
}
