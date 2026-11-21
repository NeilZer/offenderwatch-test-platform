import { createContext, ReactNode, useCallback, useContext, useEffect, useState } from 'react';
import { api, setUnauthorizedHandler } from '../api/client';
import type { User } from '../api/types';
import { runsHub } from '../realtime/hub';

/** B-07 — who is signed in, and whether they may operate (start/stop, edit, clean). */
interface AuthState {
  user: User | null;
  loading: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  const signedOut = useCallback(() => {
    setUser(null);
    void runsHub.stop();
  }, []);

  useEffect(() => {
    setUnauthorizedHandler(signedOut);
    api.me()
      .then((me) => { setUser(me); void runsHub.start(); })
      .catch(() => setUser(null))
      .finally(() => setLoading(false));
  }, [signedOut]);

  const login = useCallback(async (username: string, password: string) => {
    const me = await api.login(username, password);
    setUser(me);
    await runsHub.start();
  }, []);

  const logout = useCallback(async () => {
    await api.logout().catch(() => undefined);
    signedOut();
  }, [signedOut]);

  return <AuthContext.Provider value={{ user, loading, login, logout }}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}

/** True when the signed-in user may perform operator actions. */
export function useCanOperate(): boolean {
  return useAuth().user?.canOperate ?? false;
}
