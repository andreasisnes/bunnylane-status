import type { Level } from '../lib/types'

/** The server owns the threshold; this only maps its band to a class. `unknown` stays neutral. */
const BAND: Partial<Record<Level, string>> = { ok: 'bar-ok', degraded: 'bar-warn', down: 'bar-hot' }

export function BarBlock({ label, fraction, caption, level }: { label: string; fraction: number; caption: string; level?: Level }) {
  const pct = Math.round(Math.min(1, Math.max(0, fraction)) * 1000) / 10
  const band = (level && BAND[level]) || ''
  return (
    <div className="bar">
      <div className="bar-head">
        <span className="bar-label">{label}</span>
        <span className="bar-caption">{caption}</span>
      </div>
      <div className="bar-track" role="meter" aria-label={label} aria-valuemin={0} aria-valuemax={100} aria-valuenow={pct}>
        <div className={band ? `bar-fill ${band}` : 'bar-fill'} style={{ width: `${pct}%` }} />
      </div>
    </div>
  )
}
