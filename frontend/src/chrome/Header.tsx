import { useEffect, useState } from 'react'
import { moonPhase } from '../lib/moon'
import { Ticker } from './Ticker'


export function Header() {
  const [now, setNow] = useState(() => new Date())
  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), 1000)
    return () => clearInterval(id)
  }, [])

  return (
    <header className="chrome-header">
      <div className="brand">
        <span className="brand-name">bunnylane</span> <span className="brand-sep">▸</span> <span className="brand-page">status</span>
      </div>
      <Ticker />
      <div className="clock">
        <span className="moon" aria-hidden="true">
          {moonPhase(now)}
        </span>
        <time dateTime={now.toISOString()}>{now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</time>
      </div>
    </header>
  )
}
