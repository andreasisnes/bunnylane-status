import { forwardRef } from 'react'
import type { KeyboardEvent } from 'react'

type Props = {
  value: string
  onChange: (v: string) => void
  onKeyDown: (e: KeyboardEvent<HTMLInputElement>) => void
  hint: string[]
  busy: boolean
}

export const Prompt = forwardRef<HTMLInputElement, Props>(function Prompt({ value, onChange, onKeyDown, hint, busy }, ref) {
  return (
    <div className="prompt">
      {hint.length > 0 ? (
        <div className="prompt-hint" role="status">
          {hint.join('  ')}
        </div>
      ) : null}
      <label className="prompt-line">
        <span className="prompt-user">guest@bunnylane</span> <span className="prompt-caret">❯</span>
        <input
          ref={ref}
          aria-label="command"
          placeholder={busy ? 'running… (Ctrl+C to stop)' : 'type help'}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          onKeyDown={onKeyDown}
          autoComplete="off"
          autoCapitalize="off"
          spellCheck={false}
          autoFocus
        />
      </label>
    </div>
  )
})
