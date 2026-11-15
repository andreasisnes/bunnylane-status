import { useEffect, useMemo, useState } from 'react'

const STARS = 90

function prefersReducedMotion() {
  return typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches
}

/** The Jellyfin banner, dimmed and blurred, with a slow starfield drift unless the viewer prefers reduced motion. */
export function Background() {
  const [reduced, setReduced] = useState(prefersReducedMotion)
  useEffect(() => {
    if (typeof matchMedia !== 'function') return
    const mq = matchMedia('(prefers-reduced-motion: reduce)')
    const onChange = () => setReduced(mq.matches)
    mq.addEventListener?.('change', onChange)
    return () => mq.removeEventListener?.('change', onChange)
  }, [])

  const stars = useMemo(
    () =>
      Array.from({ length: STARS }, (_, i) => ({
        left: `${(i * 37) % 100}%`,
        top: `${(i * 53) % 100}%`,
        size: 1 + (i % 3),
        delay: `${(i % 9) * -1.3}s`,
      })),
    [],
  )

  return (
    <div className="background" data-testid="background" aria-hidden="true">
      <div className="background-banner" />
      {!reduced ? (
        <div className="starfield" data-testid="starfield">
          {stars.map((s, i) => (
            <span key={i} className="star" style={{ left: s.left, top: s.top, width: s.size, height: s.size, animationDelay: s.delay }} />
          ))}
        </div>
      ) : null}
    </div>
  )
}
