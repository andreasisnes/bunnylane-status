import { useCallback, useEffect, useRef, useState } from 'react'

export const HISTORY_KEY = 'bl.history'
const MAX = 200

function load(): string[] {
  try {
    const raw = localStorage.getItem(HISTORY_KEY)
    const parsed = raw ? (JSON.parse(raw) as unknown) : []
    return Array.isArray(parsed) ? parsed.filter((x) => typeof x === 'string').slice(-MAX) : []
  } catch {
    return []
  }
}

function save(entries: string[]) {
  try {
    localStorage.setItem(HISTORY_KEY, JSON.stringify(entries))
  } catch {
    /* storage unavailable: history is per-session only */
  }
}

/** Shell-like history: `push` on submit, `up`/`down` to walk it, `current` is the line to show. */
export function useHistory() {
  const [entries, setEntries] = useState<string[]>(load)
  const [index, setIndex] = useState<number | null>(null)
  const entriesRef = useRef(entries)
  useEffect(() => {
    entriesRef.current = entries
  }, [entries])

  const push = useCallback((line: string) => {
    const trimmed = line.trim()
    if (!trimmed) return
    setEntries((prev) => {
      if (prev[prev.length - 1] === trimmed) return prev
      const next = [...prev, trimmed].slice(-MAX)
      save(next)
      return next
    })
    setIndex(null)
  }, [])

  const up = useCallback(() => {
    setIndex((i) => {
      const len = entriesRef.current.length
      if (len === 0) return null
      return i === null ? len - 1 : Math.max(0, i - 1)
    })
  }, [])

  const down = useCallback(() => {
    setIndex((i) => (i === null || i >= entriesRef.current.length - 1 ? null : i + 1))
  }, [])

  const reset = useCallback(() => setIndex(null), [])

  return { entries, push, up, down, reset, current: index === null ? '' : entries[index] ?? '' }
}
