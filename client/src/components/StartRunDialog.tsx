import { FormEvent, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import type { Environment } from '../api/types';
import { ErrorBox, Modal } from './ui';

/** TM-02 — start a run against a chosen environment (the default one is preselected). */
export function StartRunDialog({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate();
  const [environments, setEnvironments] = useState<Environment[]>([]);
  const [environmentId, setEnvironmentId] = useState<number | null>(null);
  const [scope, setScope] = useState('all');
  const [grep, setGrep] = useState('');
  const [keepTestData, setKeepTestData] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api.environments()
      .then((envs) => {
        setEnvironments(envs);
        setEnvironmentId((envs.find((e) => e.isDefault) ?? envs[0])?.id ?? null);
      })
      .catch((e) => setError(e.message));
  }, []);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (!environmentId) return;
    setBusy(true);
    setError(null);
    try {
      const run = await api.startRun({ environmentId, scope, grep: grep.trim() || undefined, keepTestData });
      onClose();
      navigate(`/runs/${run.id}`);
    } catch (err) {
      setError((err as Error).message);
      setBusy(false);
    }
  }

  return (
    <Modal title="Start a test run" onClose={onClose}>
      <form className="form" onSubmit={submit}>
        {environments.length === 0 && !error && <p className="muted">No environment yet — create one on the Environments page first.</p>}
        <label>
          Environment
          <select value={environmentId ?? ''} onChange={(e) => setEnvironmentId(Number(e.target.value))} required>
            {environments.map((env) => (
              <option key={env.id} value={env.id}>{env.name}{env.isDefault ? ' (default)' : ''} — {env.baseUrl}</option>
            ))}
          </select>
        </label>
        <fieldset className="radios">
          <legend>What to run</legend>
          {[
            { v: 'all', l: 'Everything (API + UI)', h: '71 scenarios, about 1 minute' },
            { v: 'api', l: 'API tests only', h: '51 scenarios, about 15 seconds' },
            { v: 'ui', l: 'UI tests only', h: '20 scenarios, about 40 seconds' },
          ].map((o) => (
            <label key={o.v} className="radio">
              <input type="radio" name="scope" value={o.v} checked={scope === o.v} onChange={() => setScope(o.v)} />
              <span><b>{o.l}</b> <small className="muted">{o.h}</small></span>
            </label>
          ))}
        </fieldset>
        <label>
          Filter (optional)
          <input value={grep} onChange={(e) => setGrep(e.target.value)} placeholder="e.g. TC-21 or @FR-04" pattern="[A-Za-z0-9@\-_.]*"
            title="One word: a test id (TC-21) or a tag (@FR-04)" />
        </label>
        <label className="check">
          <input type="checkbox" checked={keepTestData} onChange={(e) => setKeepTestData(e.target.checked)} />
          <span>Leave test data for clean-up <small className="muted">— scenarios skip their own teardown; use "Clean test data" afterwards</small></span>
        </label>
        <ErrorBox message={error} />
        <div className="form-actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn primary" disabled={busy || !environmentId}>{busy ? 'Starting…' : '▶ Start run'}</button>
        </div>
      </form>
    </Modal>
  );
}
