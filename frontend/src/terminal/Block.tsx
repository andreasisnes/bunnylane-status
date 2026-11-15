import type { CommandResult } from '../lib/types'
import { BlockBody } from '../render/Blocks'

export type BlockState = 'running' | 'done' | 'streaming' | 'stopped'

export type TerminalBlock = {
  id: number
  command: string
  startedAt: Date
  state: BlockState
  result?: CommandResult
  localBlocks?: CommandResult['blocks']
}

function header(block: TerminalBlock) {
  return block.state === 'stopped' ? `${block.command} (stopped)` : block.command
}

export function Block({ block }: { block: TerminalBlock }) {
  const title = header(block)
  const blocks = block.result?.blocks ?? block.localBlocks ?? []
  const status = block.result?.status ?? (block.state === 'running' ? 'running' : 'ok')
  return (
    <article className={`block block-${block.state} status-${status}`} aria-label={title}>
      <header className="block-header">
        <span className="block-prompt">guest@bunnylane ❯</span> <span className="block-command">{title}</span>
        <time className="block-time" dateTime={block.startedAt.toISOString()}>
          {block.startedAt.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
        </time>
        <span className={`block-status status-${status}`}>{block.state === 'streaming' ? 'live' : status}</span>
      </header>
      <div className="block-body">
        {block.state === 'running' && blocks.length === 0 ? <p className="text tone-muted">…</p> : null}
        {blocks.map((b, i) => (
          <BlockBody key={i} block={b} />
        ))}
      </div>
    </article>
  )
}
