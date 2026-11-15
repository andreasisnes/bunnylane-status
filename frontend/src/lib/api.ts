import type { CommandResult, Tick } from './types'

function url(path: string, params?: Record<string, string>) {
  const u = new URL(path, window.location.origin)
  for (const [k, v] of Object.entries(params ?? {})) u.searchParams.set(k, v)
  return u.toString()
}

const offline = (line: string): CommandResult => ({ command: line, status: 'unknown', blocks: [{ type: 'text', text: 'the status service is not answering', tone: 'muted' }] })

/** Never throws: network trouble becomes an [UNKNOWN] result the terminal can show. */
export async function fetchCommand(line: string): Promise<CommandResult> {
  let response: Response
  try {
    response = await fetch(url('/api/command'), {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ line }),
    })
  } catch {
    return offline(line)
  }
  if (response.status === 429) return { command: line, status: 'error', blocks: [{ type: 'text', text: 'slow down a little', tone: 'muted' }] }
  if (!response.ok) return offline(line)
  try {
    return (await response.json()) as CommandResult
  } catch {
    return offline(line)
  }
}

type Close = () => void

/** Opens an SSE stream; `onError` fires once for a rejected line or a dropped connection. */
export function openWatch(line: string, onResult: (r: CommandResult) => void, onError: (message: string) => void): Close {
  const source = new EventSource(url('/api/watch', { line }))
  source.addEventListener('result', (e) => onResult(JSON.parse((e as MessageEvent).data) as CommandResult))
  source.addEventListener('error', (e) => {
    if ('data' in e && typeof (e as MessageEvent).data === 'string') {
      const r = JSON.parse((e as MessageEvent).data) as CommandResult
      onError(r.blocks[0]?.type === 'text' ? r.blocks[0].text : 'command not found: try help')
    } else if (source.readyState === EventSource.CLOSED) {
      onError('stream closed')
    }
    source.close()
  })
  return () => source.close()
}

export function openTicker(onTick: (t: Tick) => void): Close {
  const source = new EventSource(url('/api/ticker'))
  source.addEventListener('tick', (e) => onTick(JSON.parse((e as MessageEvent).data) as Tick))
  return () => source.close()
}
