import { FormEvent, useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { ErrorBox } from '../components/ui';

/** B-07 — sign in. Viewer: watch only. Operator: start/stop runs, edit environments, clean test data. */
export function LoginPage() {
  const { login } = useAuth();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try { await login(username.trim(), password); } catch (err) { setError((err as Error).message); setBusy(false); }
  }

  return (
    <main className="login">
      <form className="card login-card" onSubmit={submit}>
        <div className="brand big"><span className="logo">✓</span> Test Management Platform</div>
        <p className="muted">OffenderWatch QA — runs, live results, history and evidence.</p>
        <label>Username<input autoFocus autoComplete="username" value={username} onChange={(e) => setUsername(e.target.value)} required /></label>
        <label>Password<input type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} required /></label>
        <ErrorBox message={error} />
        <button className="btn primary block" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
        <p className="muted small">Two roles: <b>viewer</b> watches runs and history · <b>operator</b> also starts/stops runs, edits environments and cleans test data.</p>
      </form>
    </main>
  );
}
