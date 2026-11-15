export function BarBlock({ label, fraction, caption }: { label: string; fraction: number; caption: string }) {
  const pct = Math.round(Math.min(1, Math.max(0, fraction)) * 1000) / 10
  return (
    <div className="bar">
      <div className="bar-head">
        <span className="bar-label">{label}</span>
        <span className="bar-caption">{caption}</span>
      </div>
      <div className="bar-track" role="meter" aria-label={label} aria-valuemin={0} aria-valuemax={100} aria-valuenow={pct}>
        <div className={`bar-fill ${pct >= 90 ? 'bar-hot' : ''}`} style={{ width: `${pct}%` }} />
      </div>
    </div>
  )
}
