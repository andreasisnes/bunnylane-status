import { useCallback, useEffect, useRef, useState } from 'react'
import type { KeyboardEvent } from 'react'
import { fetchCommand, openWatch } from '../lib/api'
import { CHIPS, COMMANDS } from '../lib/commands'
import type { Block as RenderBlock, Tone } from '../lib/types'
import { Block, type TerminalBlock } from './Block'
import { complete } from './completion'
import { Prompt } from './Prompt'
import { useHistory } from './useHistory'

const VISITED_KEY = 'bl.visited'
const THEME_KEY = 'bl.theme'

function text(t: string, tone: Tone = 'plain'): RenderBlock {
  return { type: 'text', text: t, tone }
}

export function Terminal({ initialLine }: { initialLine?: string } = {}) {
  const [blocks, setBlocks] = useState<TerminalBlock[]>([])
  const [input, setInput] = useState('')
  const [hint, setHint] = useState<string[]>([])
  const history = useHistory()
  const nextId = useRef(1)
  const closeWatch = useRef<(() => void) | null>(null)
  const watchId = useRef<number | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const bottomRef = useRef<HTMLDivElement>(null)

  const update = useCallback((id: number, patch: Partial<TerminalBlock>) => {
    setBlocks((prev) => prev.map((b) => (b.id === id ? { ...b, ...patch } : b)))
  }, [])

  const stopWatch = useCallback(() => {
    if (closeWatch.current) closeWatch.current()
    closeWatch.current = null
    if (watchId.current !== null) update(watchId.current, { state: 'stopped' })
    watchId.current = null
  }, [update])

  const run = useCallback(
    async (rawLine: string) => {
      const line = rawLine.trim().replace(/\s+/g, ' ')
      if (!line) return
      history.push(line)
      const id = nextId.current++
      const [name, ...args] = line.toLowerCase().split(' ')

      if (name === 'clear') {
        setBlocks([])
        return
      }
      if (name === 'history') {
        setBlocks((prev) => [...prev, { id, command: line, startedAt: new Date(), state: 'done', localBlocks: [text(history.entries.join('\n') || '(empty)', 'muted')] }])
        return
      }
      if (name === 'theme') {
        const theme = args[0] === 'dawn' ? 'dawn' : 'night'
        document.documentElement.dataset.theme = theme
        try {
          localStorage.setItem(THEME_KEY, theme)
        } catch {
          /* ignore */
        }
        setBlocks((prev) => [...prev, { id, command: line, startedAt: new Date(), state: 'done', localBlocks: [text(`theme: ${theme}`, 'muted')] }])
        return
      }

      if (name === 'watch') {
        stopWatch()
        const target = args.join(' ')
        setBlocks((prev) => [...prev, { id, command: line, startedAt: new Date(), state: 'streaming' }])
        watchId.current = id
        closeWatch.current = openWatch(
          target,
          (result) => update(id, { result }),
          (message) => {
            update(id, { state: 'stopped', localBlocks: [text(message, 'red')] })
            closeWatch.current = null
            watchId.current = null
          },
        )
        return
      }

      setBlocks((prev) => [...prev, { id, command: line, startedAt: new Date(), state: 'running' }])
      const result = await fetchCommand(line)
      update(id, { result, state: 'done' })
    },
    [history, stopWatch, update],
  )

  useEffect(() => {
    try {
      const theme = localStorage.getItem(THEME_KEY)
      if (theme) document.documentElement.dataset.theme = theme
      if (initialLine) {
        void run(initialLine)
      } else if (!localStorage.getItem(VISITED_KEY)) {
        localStorage.setItem(VISITED_KEY, '1')
        void run('help')
      }
    } catch {
      void run('help')
    }
    return () => closeWatch.current?.()
    // first-visit behaviour runs once
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    if (typeof bottomRef.current?.scrollIntoView === 'function') bottomRef.current.scrollIntoView({ block: 'end' })
  }, [blocks])

  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.ctrlKey && e.key.toLowerCase() === 'c') {
      e.preventDefault()
      stopWatch()
      setInput('')
      return
    }
    if (e.ctrlKey && e.key.toLowerCase() === 'l') {
      e.preventDefault()
      setBlocks([])
      return
    }
    switch (e.key) {
      case 'Enter': {
        e.preventDefault()
        setHint([])
        const line = input
        setInput('')
        if (line.trim() === 'stop') {
          stopWatch()
          return
        }
        void run(line)
        return
      }
      case 'Tab': {
        e.preventDefault()
        const options = complete(input, COMMANDS)
        if (options.length === 1) {
          const tokens = input.split(/\s+/)
          tokens[tokens.length - 1] = options[0]
          setInput(tokens.join(' '))
          setHint([])
        } else {
          setHint(options)
        }
        return
      }
      case 'Escape':
        setHint([])
        return
      case 'ArrowUp':
        e.preventDefault()
        history.up()
        return
      case 'ArrowDown':
        e.preventDefault()
        history.down()
        return
    }
  }

  // Reflect history navigation into the input.
  const lastHistory = useRef('')
  useEffect(() => {
    if (history.current !== lastHistory.current) {
      lastHistory.current = history.current
      setInput(history.current)
    }
  }, [history])

  return (
    <section className="terminal" onClick={() => inputRef.current?.focus()}>
      <div className="blocks">
        {blocks.map((b) => (
          <Block key={b.id} block={b} />
        ))}
        <div ref={bottomRef} />
      </div>
      <div className="chips" role="group" aria-label="quick commands">
        {CHIPS.map((c) => (
          <button key={c} type="button" className="chip" onClick={() => void run(c)}>
            {c}
          </button>
        ))}
      </div>
      <Prompt ref={inputRef} value={input} onChange={(v) => { setInput(v); setHint([]) }} onKeyDown={onKeyDown} hint={hint} busy={watchId.current !== null} />
    </section>
  )
}
