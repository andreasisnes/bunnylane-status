import { COMMANDS } from '../lib/commands'
import { hhmmss } from '../lib/time'
import type { CommandResult } from '../lib/types'
import { BlockBody } from '../render/Blocks'

export type BlockState = 'running' | 'done' | 'streaming' | 'stopped'

export type TerminalBlock = {
  id: number
  /** The line as the user sees it, e.g. "net 24h". */
  command: string
  startedAt: Date
  state: BlockState
  result?: CommandResult
  localBlocks?: CommandResult['blocks']
  /** Local commands (help, clear, …) cannot go live. */
  local?: boolean
}

export type BlockActions = {
  goLive: (id: number) => void
  stop: (id: number) => void
  rerun: (id: number) => void
  close: (id: number) => void
  setArg: (id: number, arg: string) => void
  pick: (command: string) => void
}

function header(block: TerminalBlock) {
  return block.state === 'stopped' ? `${block.command} (stopped)` : block.command
}

export function Block({ block, actions }: { block: TerminalBlock; actions: BlockActions }) {
  const title = header(block)
  const blocks = block.result?.blocks ?? block.localBlocks ?? []
  const status = block.result?.status ?? (block.state === 'running' ? 'running' : 'ok')
  const [name, currentArg] = block.command.split(' ')
  const spec = COMMANDS.find((c) => c.name === name)
  const canLive = !block.local && spec && !spec.local && name !== 'help'
  const isLive = block.state === 'streaming'

  return (
    <article className={`block block-${block.state} status-${status}`} aria-label={title}>
      <header className="block-header">
        <span className="block-prompt">guest@bunnylane ❯</span> <span className="block-command">{title}</span>
        {isLive ? <span className="block-live">live</span> : null}
        <time className="block-time" dateTime={block.startedAt.toISOString()}>
          {hhmmss(block.startedAt)}
        </time>
        <span className={`block-status status-${status}`}>{status}</span>
        <span className="block-actions">
          {canLive && !isLive ? (
            <button type="button" className="block-action" onClick={() => actions.goLive(block.id)} title="refresh every 2 s">
              go live
            </button>
          ) : null}
          {isLive ? (
            <button type="button" className="block-action" onClick={() => actions.stop(block.id)}>
              stop
            </button>
          ) : null}
          {!block.local && !isLive ? (
            <button type="button" className="block-action" onClick={() => actions.rerun(block.id)}>
              re-run
            </button>
          ) : null}
          <button type="button" className="block-action" onClick={() => actions.close(block.id)} aria-label="close" title="close">
            ✕
          </button>
        </span>
      </header>
      {spec && spec.args.length > 0 && !spec.local ? (
        <div className="block-args" role="group" aria-label={`${name} options`}>
          {spec.args.map((a) => (
            <button
              key={a}
              type="button"
              className={`arg-chip ${currentArg === a ? 'arg-chip-active' : ''}`}
              aria-pressed={currentArg === a}
              onClick={() => actions.setArg(block.id, a)}
            >
              {a}
            </button>
          ))}
        </div>
      ) : null}
      <div className="block-body">
        {block.state === 'running' && blocks.length === 0 ? <p className="text tone-muted">…</p> : null}
        {blocks.map((b, i) => (
          <BlockBody key={i} block={b} onPick={name === 'help' ? actions.pick : undefined} />
        ))}
      </div>
    </article>
  )
}
