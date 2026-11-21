import { NavLink, Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';
import { LiveIndicator, Loading } from './components/ui';
import { ComparePage } from './pages/ComparePage';
import { DashboardPage } from './pages/DashboardPage';
import { EnvironmentsPage } from './pages/EnvironmentsPage';
import { LoginPage } from './pages/LoginPage';
import { RunDetailPage } from './pages/RunDetailPage';
import { RunsPage } from './pages/RunsPage';
import { SchedulesPage } from './pages/SchedulesPage';
import { SettingsPage } from './pages/SettingsPage';
import { TestDataPage } from './pages/TestDataPage';
import { TestHistoryPage } from './pages/TestHistoryPage';
import { TestsPage } from './pages/TestsPage';

const NAV = [
  { to: '/', label: 'Dashboard', icon: '◎', end: true },
  { to: '/runs', label: 'Runs', icon: '▶' },
  { to: '/tests', label: 'Test history', icon: '≡' },
  { to: '/compare', label: 'Compare runs', icon: '⇄' },
  { to: '/test-data', label: 'Test data', icon: '🧹' },
  { to: '/environments', label: 'Environments', icon: '⌂' },
  { to: '/schedules', label: 'Schedules', icon: '⏱' },
  { to: '/settings', label: 'Notifications', icon: '🔔' },
];

export function App() {
  const { user, loading, logout } = useAuth();
  if (loading) return <Loading />;
  if (!user) return <LoginPage />;

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand"><span className="logo">✓</span> Test Platform</div>
        <nav>
          {NAV.map((n) => (
            <NavLink key={n.to} to={n.to} end={n.end} className={({ isActive }) => (isActive ? 'active' : '')}>
              <span className="nav-icon" aria-hidden="true">{n.icon}</span>{n.label}
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-foot">
          <LiveIndicator />
          <div className="user">
            <span>{user.username}</span>
            <span className={`role role-${user.role}`}>{user.role}</span>
          </div>
          <button className="link" onClick={() => void logout()}>Sign out</button>
          <a className="link small" href="/swagger" target="_blank" rel="noreferrer">API (Swagger)</a>
        </div>
      </aside>
      <main className="content">
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/runs" element={<RunsPage />} />
          <Route path="/runs/:id" element={<RunDetailPage />} />
          <Route path="/tests" element={<TestsPage />} />
          <Route path="/tests/:id" element={<TestHistoryPage />} />
          <Route path="/compare" element={<ComparePage />} />
          <Route path="/test-data" element={<TestDataPage />} />
          <Route path="/environments" element={<EnvironmentsPage />} />
          <Route path="/schedules" element={<SchedulesPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  );
}
