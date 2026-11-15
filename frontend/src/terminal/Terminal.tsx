import { useCallback, useEffect, useRef, useState } from 'react'
import type { KeyboardEvent } from 'react'
import { fetchCommand, openWatch } from '../lib/api'
import { CHIPS, COMMANDS } from '../lib/commands'
import type { Block as RenderBlock, Tone } from '../lib/types'
import { Block, type BlockActions, type TerminalBlock } from './Block'
import { complete } from './completion'
import { Prompt } from './Prompt'
import { useHistory } from './useHistory'

const VISITED_KEY = 'bl.visited'
const THEME_KEY = 'bl.theme'
const MAX_BLOCKS = 200

function text(t: string, tone: Tone = 'plain'): RenderBlock {
  return { type: 'text', text: t, tone }
}

function normalise(raw: string) {
  return raw.trim().replace(/\s+/g, ' ').toLowerCase()
}

export function Terminal({ initialLine }: { initialLine?: string } = {}) {
  const [blocks, setBlocks] = useState<TerminalBlock[]>([])
  const [input, setInput] = useState('')
  const [hint, setHint] = useState<string[]>([])
  const [liveId, setLiveId] = useState<number | null>(null)
  const history = useHistory()
  const nextId = useRef(1)
  const closeWatch = useRef<(() => void) | null>(null)
  const liveRef = useRef<number | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const bottomRef = useRef<HTMLDivElement>(null)

  const update = useCallback((id: number, patch: Partial<TerminalBlock>) => {
    setBlocks((prev) => prev.map((b) => (b.id === id ? { ...b, ...patch } : b)))
  }, [])

  const append = useCallback((block: TerminalBlock) => {
    setBlocks((prev) => [...prev, block].slice(-MAX_BLOCKS))
  }, [])

  const setLive = (id: number | null) => {
    liveRef.current = id
    setLiveId(id)
  }

  /** Stops the one live stream, marking its block stopped. */
  const stopLive = useCallback(() => {
    closeWatch.current?.()
    closeWatch.current = null
    if (liveRef.current !== null) update(liveRef.current, { state: 'stopped' })
    setLive(null)
  }, [update])

  /** Streams `line` into block `id` every 2 s; only one block is live at a time. */
  const startLive = useCallback(
    (id: number, line: string) => {
      stopLive()
      update(id, { command: line, state: 'streaming' })
      setLive(id)
      closeWatch.current = openWatch(
        line,
        (result) => update(id, { result }),
        (message) => {
          update(id, { state: 'stopped', localBlocks: [text(message, 'red')] })
          closeWatch.current = null
          setLive(null)
        },
      )
    },
    [stopLive, update],
  )

  const runOnce = useCallback(
    async (id: number, line: string) => {
      update(id, { command: line, state: 'running', result: undefined, localBlocks: undefined })
      const result = await fetchCommand(line)
      update(id, { result, state: 'done' })
    },
    [update],
  )

  const runLocal = useCallback(
    (id: number, name: string, args: string[], line: string) => {
      if (name === 'clear') {
        stopLive()
        setBlocks([])
        return true
      }
      if (name === 'history') {
        append({ id, command: line, startedAt: new Date(), state: 'done', local: true, localBlocks: [text(history.entries.join('\n') || '(empty)', 'muted')] })
        return true
      }
      if (name === 'theme') {
        const theme = args[0] === 'dawn' ? 'dawn' : 'night'
        document.documentElement.dataset.theme = theme
        try {
          localStorage.setItem(THEME_KEY, theme)
        } catch {
          /* ignore */
        }
        append({ id, command: line, startedAt: new Date(), state: 'done', local: true, localBlocks: [text(`theme: ${theme}`, 'muted')] })
        return true
      }
      if (name === 'stop') {
        stopLive()
        return true
      }
      return false
    },
    [append, history.entries, stopLive],
  )

  /** A typed line: local commands, `watch x` (live), anything else once. */
  const submit = useCallback(
    (rawLine: string, opts: { live?: boolean } = {}) => {
      const line = normalise(rawLine)
      if (!line) return
      history.push(line)
      const id = nextId.current++
      const [name, ...args] = line.split(' ')
      if (runLocal(id, name, args, line)) return

      const target = name === 'watch' ? args.join(' ') : line
      const spec = COMMANDS.find((c) => c.name === target.split(' ')[0])
      const live = name === 'watch' || (opts.live === true && !!spec && !spec.local && spec.name !== 'help')

      append({ id, command: target, startedAt: new Date(), state: live ? 'streaming' : 'running', local: !!spec?.local || target === 'help' })
      if (live) startLive(id, target)
      else void runOnce(id, target)
    },
    [append, history, runLocal, runOnce, startLive],
  )

  const actions: BlockActions = {
    goLive: (id) => {
      const b = blocks.find((x) => x.id === id)
      if (b) startLive(id, b.command)
    },
    stop: (id) => {
      if (liveRef.current === id) stopLive()
    },
    rerun: (id) => {
      const b = blocks.find((x) => x.id === id)
      if (b) void runOnce(id, b.command)
    },
    close: (id) => {
      if (liveRef.current === id) stopLive()
      setBlocks((prev) => prev.filter((b) => b.id !== id))
    },
    setArg: (id, arg) => {
      const b = blocks.find((x) => x.id === id)
      if (!b) return
      const line = `${b.command.split(' ')[0]} ${arg}`
      if (b.state === 'streaming') startLive(id, line)
      else void runOnce(id, line)
    },
    pick: (command) => submit(command, { live: true }),
  }

  useEffect(() => {
    try {
      const theme = localStorage.getItem(THEME_KEY)
      if (theme) document.documentElement.dataset.theme = theme
      if (initialLine) {
        submit(initialLine)
      } else if (!localStorage.getItem(VISITED_KEY)) {
        localStorage.setItem(VISITED_KEY, '1')
        submit('help')
      }
    } catch {
      submit('help')
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
      stopLive()
      setInput('')
      return
    }
    if (e.ctrlKey && e.key.toLowerCase() === 'l') {
      e.preventDefault()
      stopLive()
      setBlocks([])
      return
    }
    switch (e.key) {
      case 'Enter': {
        e.preventDefault()
        setHint([])
        const line = input
        setInput('')
        submit(line)
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

  const lastHistory = useRef('')
  useEffect(() => {
    if (history.current !== lastHistory.current) {
      lastHistory.current = history.current
      setInput(history.current)
    }
  }, [history])

  return (
    <section className="terminal" onClick={(e) => { if ((e.target as HTMLElement).closest('button, a, input, .u-over') === null) inputRef.current?.focus() }}>
      <div className="blocks">
        {blocks.map((b) => (
          <Block key={b.id} block={b} actions={actions} />
        ))}
        <div ref={bottomRef} />
      </div>
      <div className="chips" role="group" aria-label="quick commands">
        {CHIPS.map((c) => (
          <button key={c} type="button" className="chip" onClick={() => submit(c, { live: true })} title={`live ${c}`}>
            {c}
          </button>
        ))}
      </div>
      <Prompt ref={inputRef} value={input} onChange={(v) => { setInput(v); setHint([]) }} onKeyDown={onKeyDown} hint={hint} busy={liveId !== null} />
    </section>
  )
}
