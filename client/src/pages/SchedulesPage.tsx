import { FormEvent, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, ScheduleInput } from '../api/client';
import type { Environment, Schedule } from '../api/types';
import { useCanOperate } from '../auth/AuthContext';
import { Empty, ErrorBox, Loading, Modal, OperatorButton, PageHeader } from '../components/ui';
import { formatDateTime } from '../lib/format';
import { useAsync } from '../lib/useAsync';

/** B-04 — recurring runs. Stored in the database, so they survive an API restart; runs are marked "Scheduled". */
const PRESETS = [
  { label: 'Every night at 02:00', cron: '0 2 * * *' },
  { label: 'Weekdays at 08:00', cron: '0 8 * * 1-5' },
  { label: 'Every hour', cron: '0 * * * *' },
  { label: 'Every 15 minutes', cron: '*/15 * * * *' },
];

export function SchedulesPage() {
  const canOperate = useCanOperate();
  const { data, error, loading, reload } = useAsync(() => api.schedules(), []);
  const [editing, setEditing] = useState<Schedule | 'new' | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  async function remove(s: Schedule) {
    setActionError(null);
    try { await api.deleteSchedule(s.id); await reload(); } catch (e) { setActionError((e as Error).message); }
  }

  return (
    <>
      <PageHeader title="Schedules" subtitle="Recurring runs, e.g. every night against Staging. They survive a platform restart."
        actions={<OperatorButton canOperate={canOperate} className="btn primary" onClick={() => setEditing('new')}>+ Add schedule</OperatorButton>} />
      <ErrorBox message={actionError ?? error} />
      {loading && !data ? <Loading /> : !data?.length ? <Empty>No schedules yet.</Empty> : (
        <div className="card table-wrap">
          <table className="table">
            <thead><tr><th>Name</th><th>When</th><th>Environment</th><th>Scope</th><th>Next run</th><th>Last run</th><th /></tr></thead>
            <tbody>{data.map((s) => (
              <tr key={s.id}>
                <td><b>{s.name}</b> {!s.enabled && <span className="pill neutral">Paused</span>}</td>
                <td><code>{s.cron}</code> <small className="muted">{describeCron(s.cron)} · {s.timeZone}</small></td>
                <td>{s.environmentName}</td>
                <td>{s.scope}</td>
                <td className="nowrap">{s.enabled ? formatDateTime(s.nextRunAt) : '—'}</td>
                <td className="nowrap">{s.lastRunId ? <Link to={`/runs/${s.lastRunId}`}>#{s.lastRunId}</Link> : '—'} <small className="muted">{formatDateTime(s.lastRunAt)}</small></td>
                <td className="row-actions">
                  <OperatorButton canOperate={canOperate} className="btn small" onClick={() => setEditing(s)}>Edit</OperatorButton>
                  <OperatorButton canOperate={canOperate} className="btn small danger" onClick={() => remove(s)}>Delete</OperatorButton>
                </td>
              </tr>
            ))}</tbody>
          </table>
        </div>
      )}
      {editing && <ScheduleForm schedule={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={reload} />}
    </>
  );
}

function ScheduleForm({ schedule, onClose, onSaved }: { schedule: Schedule | null; onClose: () => void; onSaved: () => void }) {
  const [environments, setEnvironments] = useState<Environment[]>([]);
  const [form, setForm] = useState<ScheduleInput>({
    name: schedule?.name ?? 'Nightly regression', cron: schedule?.cron ?? '0 2 * * *',
    timeZone: schedule?.timeZone ?? Intl.DateTimeFormat().resolvedOptions().timeZone,
    environmentId: schedule?.environmentId ?? 0, scope: schedule?.scope ?? 'all', enabled: schedule?.enabled ?? true,
  });
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.environments().then((envs) => {
      setEnvironments(envs);
      if (!form.environmentId) setForm((f) => ({ ...f, environmentId: (envs.find((e) => e.isDefault) ?? envs[0])?.id ?? 0 }));
    }).catch((e) => setError(e.message));
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      if (schedule) await api.updateSchedule(schedule.id, form);
      else await api.createSchedule(form);
      onSaved();
      onClose();
    } catch (err) { setError((err as Error).message); }
  }

  return (
    <Modal title={schedule ? `Edit ${schedule.name}` : 'Add schedule'} onClose={onClose}>
      <form className="form" onSubmit={submit}>
        <label>Name<input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></label>
        <label>Environment
          <select value={form.environmentId} onChange={(e) => setForm({ ...form, environmentId: Number(e.target.value) })}>
            {environments.map((env) => <option key={env.id} value={env.id}>{env.name}</option>)}
          </select>
        </label>
        <label>Scope
          <select value={form.scope} onChange={(e) => setForm({ ...form, scope: e.target.value })}>
            <option value="all">Everything (API + UI)</option><option value="api">API only</option><option value="ui">UI only</option>
          </select>
        </label>
        <div className="presets">{PRESETS.map((p) => (
          <button type="button" key={p.cron} className={`chip ${form.cron === p.cron ? 'active' : ''}`} onClick={() => setForm({ ...form, cron: p.cron })}>{p.label}</button>
        ))}</div>
        <label>Cron expression (minute hour day month weekday)
          <input required value={form.cron} onChange={(e) => setForm({ ...form, cron: e.target.value })} />
          <small className="muted">{describeCron(form.cron)}</small>
        </label>
        <label>Time zone<input value={form.timeZone} onChange={(e) => setForm({ ...form, timeZone: e.target.value })} /></label>
        <label className="check"><input type="checkbox" checked={form.enabled} onChange={(e) => setForm({ ...form, enabled: e.target.checked })} /> Enabled</label>
        <ErrorBox message={error} />
        <div className="form-actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn primary">Save</button>
        </div>
      </form>
    </Modal>
  );
}

function describeCron(cron: string): string {
  return PRESETS.find((p) => p.cron === cron.trim())?.label.toLowerCase() ?? 'custom schedule';
}
