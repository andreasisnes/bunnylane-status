import { useEffect, useState } from 'react'
import { openTicker } from '../lib/api'
import { formatBits, formatBytes } from '../lib/format'
import type { Tick } from '../lib/types'

export function Ticker() {
  const [tick, setTick] = useState<Tick | null>(null)

  useEffect(() => openTicker(setTick), [])

  const wan = tick && tick.wanInBps !== null && tick.wanOutBps !== null ? `↓${formatBits(tick.wanInBps)} ↑${formatBits(tick.wanOutBps)}` : '— / —'
  const vps = tick && tick.vpsUsedBytes !== null ? `${formatBytes(tick.vpsUsedBytes)} / ${formatBytes(tick.vpsCapBytes)}` : '—'
  const streams = tick && tick.streams !== null ? `${tick.streams} ${tick.streams === 1 ? 'stream' : 'streams'}` : '— streams'

  return (
    <div className="ticker" aria-live="off">
      <span className="ticker-item">wan {wan}</span>
      <span className="ticker-item">vps {vps}</span>
      <span className="ticker-item">{streams}</span>
    </div>
  )
}
