import { useEffect, useRef } from 'react'
import uPlot from 'uplot'
import 'uplot/dist/uPlot.min.css'
import { formatUnit } from '../lib/format'
import type { Series } from '../lib/types'

type Props = { title: string; unit: string; timestamps: number[]; series: Series[] }

const AMBER = '#d4922a'
const MOON = '#5a9abf'
const GRID = 'rgba(237, 232, 223, 0.08)'
const TEXT = '#7a8ba0'

function canvasAvailable() {
  try {
    return typeof document !== 'undefined' && !!document.createElement('canvas').getContext('2d')
  } catch {
    return false
  }
}

/** A thin amber line (and a moon-blue second series) on a dotted grid, drawn with uPlot. */
export function ChartBlock({ title, unit, timestamps, series }: Props) {
  const host = useRef<HTMLDivElement>(null)
  const plot = useRef<uPlot | null>(null)
  const shape = useRef<string>('')
  const raf = useRef<number | null>(null)
  const reduced = typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches

  // Build once per series shape (names + unit); later ticks only push data, so `watch` never strobes.
  useEffect(() => {
    if (!host.current || !canvasAvailable() || timestamps.length === 0) return
    const data: uPlot.AlignedData = [timestamps, ...series.map((s) => s.values)]
    const nextShape = `${unit}|${series.map((s) => s.name).join(',')}`

    if (plot.current && shape.current === nextShape) {
      plot.current.setData(data)
      return
    }

    plot.current?.destroy()
    if (raf.current !== null) cancelAnimationFrame(raf.current)
    shape.current = nextShape
    const width = host.current.clientWidth || 640
    const opts: uPlot.Options = {
      width,
      height: 320,
      cursor: { points: { size: 6 } },
      legend: { show: true },
      scales: { x: { time: true }, y: { range: (_u, min, max) => [Math.min(0, min), max <= 0 ? 1 : max * 1.1] } },
      axes: [
        { stroke: TEXT, grid: { stroke: GRID, dash: [2, 4] }, ticks: { stroke: GRID }, font: '11px "DM Mono", monospace' },
        { stroke: TEXT, grid: { stroke: GRID, dash: [2, 4] }, ticks: { stroke: GRID }, font: '11px "DM Mono", monospace', size: 72, values: (_u, vals) => vals.map((v) => formatUnit(v, unit)) },
      ],
      series: [
        { label: 'time' },
        ...series.map((s, i) => ({
          label: s.name,
          stroke: i === 0 ? AMBER : MOON,
          width: 1.5,
          fill: i === 0 ? 'rgba(212, 146, 42, 0.08)' : undefined,
          spanGaps: false,
          value: (_u: uPlot, v: number | null) => (v === null ? '—' : formatUnit(v, unit)),
        })),
      ],
    }
    plot.current = new uPlot(opts, data, host.current)
    if (!reduced) {
      // draw-in on first paint only: reveal left to right over ~600 ms
      const start = performance.now()
      const total = timestamps.length
      const step = (t: number) => {
        const f = Math.min(1, (t - start) / 600)
        plot.current?.setData([timestamps, ...series.map((s) => s.values.map((v, i) => (i <= f * total ? v : null)))])
        raf.current = f < 1 ? requestAnimationFrame(step) : null
      }
      raf.current = requestAnimationFrame(step)
    }
  }, [timestamps, series, unit, reduced])

  // Resize with the panel; destroy on unmount.
  useEffect(() => {
    if (!host.current || typeof ResizeObserver === 'undefined') return
    const el = host.current
    const ro = new ResizeObserver(() => plot.current?.setSize({ width: el.clientWidth || 640, height: 320 }))
    ro.observe(el)
    return () => {
      ro.disconnect()
      if (raf.current !== null) cancelAnimationFrame(raf.current)
      plot.current?.destroy()
      plot.current = null
      shape.current = ''
    }
  }, [])

  const summary = series.map((s) => {
    const last = [...s.values].reverse().find((v) => v !== null)
    return `${s.name} ${last === null || last === undefined ? '—' : formatUnit(last, unit)}`
  })

  return (
    <figure className="chart" aria-label={title}>
      <figcaption className="chart-title">
        {title} <span className="chart-summary">{summary.join(' · ')}</span>
      </figcaption>
      <div ref={host} className="chart-host" />
    </figure>
  )
}
