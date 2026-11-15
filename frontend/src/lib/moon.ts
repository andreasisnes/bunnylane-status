const PHASES = ['🌑', '🌒', '🌓', '🌔', '🌕', '🌖', '🌗', '🌘']

/** Approximate lunar phase from a known new moon (2000-01-06 18:14 UTC), good to a day. */
export function moonPhase(date: Date) {
  const synodic = 29.530588853
  const days = (date.getTime() - Date.UTC(2000, 0, 6, 18, 14)) / 86_400_000
  const phase = ((days % synodic) + synodic) % synodic
  return PHASES[Math.round((phase / synodic) * 8) % 8]
}
