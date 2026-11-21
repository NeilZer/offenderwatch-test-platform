import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import type { TrendPoint } from '../api/types';
import { formatDateTime, scopeLabel } from '../lib/format';

/**
 * TM-07 — pass rate of every completed run, one line per environment (max 3 categorical colours).
 * Complete runs are filled points; partial runs (API only, UI only, filtered, incomplete) are HOLLOW,
 * because their pass rate covers only part of the suite. A switch hides them.
 * Hand-drawn SVG: one y-axis (0–100 %), hover tooltip, direct labels, and a table view.
 */
const SERIES = ['var(--s1)', 'var(--s2)', 'var(--s3)'];

type Mode = 'all' | 'complete';

export function TrendChart({ points: allPoints }: { points: TrendPoint[] }) {
  const [hover, setHover] = useState<TrendPoint | null>(null);
  const [asTable, setAsTable] = useState(false);
  const [mode, setMode] = useState<Mode>('all');
  const points = useMemo(() => (mode === 'all' ? allPoints : allPoints.filter((p) => p.isFullSuite)), [allPoints, mode]);
  const envs = useMemo(() => [...new Set(allPoints.map((p) => p.environmentName))].slice(0, 3), [allPoints]);

  if (allPoints.length === 0) return <p className="muted">No completed runs yet.</p>;

  return (
    <div className="trend">
      <div className="trend-head">
        <ul className="legend">
          {envs.map((env, i) => <li key={env}><i className="sw" style={{ background: SERIES[i] }} />{env}</li>)}
          <li><i className="sw hollow" />partial run</li>
        </ul>
        <div className="actions">
          <div className="segmented" role="group" aria-label="Which runs">
            <button className={mode === 'all' ? 'active' : ''} onClick={() => setMode('all')}>All runs ({allPoints.length})</button>
            <button className={mode === 'complete' ? 'active' : ''} onClick={() => setMode('complete')}>
              Complete runs only ({allPoints.filter((p) => p.isFullSuite).length})
            </button>
          </div>
          <button className="link" onClick={() => setAsTable(!asTable)}>{asTable ? 'Show chart' : 'Show as table'}</button>
        </div>
      </div>
      {points.length === 0 ? <p className="muted">No complete run yet.</p> : asTable ? <TrendTable points={points} /> : (
        <TrendSvg points={points} envs={envs} hover={hover} setHover={setHover} />
      )}
    </div>
  );
}

function kindOf(p: TrendPoint): string {
  if (p.isFullSuite) return 'Complete';
  if (p.skipped > 0 && p.scope === 'all' && !p.filter) return `Incomplete — ${p.skipped} skipped`;
  return `Partial — ${scopeLabel(p)}`;
}

function TrendTable({ points }: { points: TrendPoint[] }) {
  return (
    <div className="table-wrap">
      <table className="table compact">
        <thead><tr><th>Run</th><th>Environment</th><th>Kind</th><th>Finished</th><th>Pass rate</th><th>Passed / executed</th></tr></thead>
        <tbody>{points.map((p) => (
          <tr key={p.runId} className={p.isFullSuite ? '' : 'muted'}>
            <td><Link to={`/runs/${p.runId}`}>#{p.runId}</Link></td>
            <td>{p.environmentName}</td>
            <td>{kindOf(p)}</td>
            <td className="nowrap">{formatDateTime(p.at)}</td>
            <td>{p.passRate}%</td>
            <td>{p.passed} / {p.executed}</td>
          </tr>
        ))}</tbody>
      </table>
    </div>
  );
}

function TrendSvg({ points, envs, hover, setHover }: {
  points: TrendPoint[]; envs: string[]; hover: TrendPoint | null; setHover: (p: TrendPoint | null) => void;
}) {
  const width = 640, height = 200, left = 36, right = 12, top = 12, bottom = 26;
  const x = (i: number) => left + (points.length === 1 ? (width - left - right) / 2 : (i / (points.length - 1)) * (width - left - right));
  const y = (rate: number) => top + (1 - rate / 100) * (height - top - bottom);

  return (
    <div className="chart-wrap">
      <svg viewBox={`0 0 ${width} ${height}`} className="chart" role="img" aria-label="Pass rate per run">
        {[0, 25, 50, 75, 100].map((g) => (
          <g key={g}>
            <line x1={left} x2={width - right} y1={y(g)} y2={y(g)} className="grid" />
            <text x={left - 6} y={y(g) + 4} className="axis" textAnchor="end">{g}%</text>
          </g>
        ))}
        {envs.map((env, si) => {
          const series = points.map((p, i) => ({ p, i })).filter(({ p }) => p.environmentName === env);
          if (series.length === 0) return null;
          const d = series.map(({ p, i }, k) => `${k ? 'L' : 'M'}${x(i)},${y(p.passRate)}`).join(' ');
          const last = series[series.length - 1];
          return (
            <g key={env}>
              <path d={d} fill="none" stroke={SERIES[si]} strokeWidth={2} strokeLinejoin="round" opacity={0.85} />
              {series.map(({ p, i }) => (
                <circle key={p.runId} cx={x(i)} cy={y(p.passRate)} r={hover?.runId === p.runId ? 6 : 4}
                  fill={p.isFullSuite ? SERIES[si] : 'var(--surface)'} stroke={p.isFullSuite ? 'var(--surface)' : SERIES[si]} strokeWidth={2} />
              ))}
              <text x={x(last.i) - 8} y={last.p.passRate > 85 ? y(last.p.passRate) + 18 : y(last.p.passRate) - 8} className="direct" textAnchor="end">
                {last.p.passRate}%
              </text>
            </g>
          );
        })}
        {points.map((p, i) => (
          <rect key={p.runId} x={x(i) - 12} y={top} width={24} height={height - top - bottom} fill="transparent"
            onMouseEnter={() => setHover(p)} onMouseLeave={() => setHover(null)} />
        ))}
        <text x={left} y={height - 6} className="axis">older</text>
        <text x={width - right} y={height - 6} className="axis" textAnchor="end">newest run</text>
      </svg>
      {hover && (
        <div className="tooltip">
          <b>Run #{hover.runId}</b> · {hover.environmentName}<br />
          {kindOf(hover)}<br />
          {hover.passRate}% pass ({hover.passed}/{hover.executed})<br />
          <span className="muted">{formatDateTime(hover.at)}</span>
        </div>
      )}
    </div>
  );
}
