import { spark } from '../lib/format'

export function SparklineBlock({ label, values, unit }: { label: string; values: number[]; unit: string }) {
  const last = values.length ? values[values.length - 1] : null
  return (
    <div className="sparkline">
      <span className="sparkline-label">{label}</span>
      <span className="sparkline-glyphs" aria-label={label}>
        {spark(values)}
      </span>
      {last !== null ? (
        <span className="sparkline-last">
          {unit === 'GB' ? `${last.toFixed(1)} ${unit}` : `${Math.round(last)} ${unit}`}
        </span>
      ) : null}
    </div>
  )
}
