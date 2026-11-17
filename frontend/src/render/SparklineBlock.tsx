import { downsample } from '../lib/downsample'
import { formatUnit, spark } from '../lib/format'

export function SparklineBlock({ label, values, unit }: { label: string; values: number[]; unit: string }) {
  const last = values.length ? values[values.length - 1] : null
  return (
    <div className="sparkline">
      <span className="sparkline-label">{label}</span>
      <span className="sparkline-glyphs" aria-label={label}>
        {spark(downsample(values))}
      </span>
      {last !== null ? (
        <span className="sparkline-last">{formatUnit(last, unit)}</span>
      ) : null}
    </div>
  )
}
