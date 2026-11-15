const BYTE_UNITS = ['B', 'kB', 'MB', 'GB', 'TB', 'PB']
const BIT_UNITS = ['bit/s', 'kbit/s', 'Mbit/s', 'Gbit/s', 'Tbit/s']

function scaled(value: number, units: string[]) {
  let v = Math.max(0, value)
  let i = 0
  while (v >= 1000 && i < units.length - 1) {
    v /= 1000
    i++
  }
  const text = i === 0 ? v.toFixed(0) : v >= 100 ? v.toFixed(0) : v >= 10 ? v.toFixed(1) : v.toFixed(2)
  return `${text} ${units[i]}`
}

export const formatBytes = (b: number) => scaled(b, BYTE_UNITS)
export const formatBits = (bps: number) => scaled(bps, BIT_UNITS)
export const formatPercent = (f: number) => `${Math.round(Math.min(1, Math.max(0, f)) * 100)}%`

const SPARK = '▁▂▃▄▅▆▇█'
export function spark(values: number[]) {
  if (values.length === 0) return ''
  const min = Math.min(...values)
  const max = Math.max(...values)
  const range = max - min
  return values.map((v) => SPARK[range <= 0 ? 0 : Math.round(((v - min) / range) * (SPARK.length - 1))]).join('')
}

/** Format a value in the chart's unit for axes and summaries. */
export function formatUnit(value: number, unit: string) {
  if (unit === 'bit/s') return formatBits(value)
  if (unit === 'B') return formatBytes(value)
  if (unit === '%') return `${Math.round(value)}%`
  if (unit === 'GB') return `${value.toFixed(1)} GB`
  return `${value}`
}
