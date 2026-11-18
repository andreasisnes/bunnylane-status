export type Tone = 'plain' | 'muted' | 'amber' | 'moon' | 'red'
export type Level = 'ok' | 'degraded' | 'down' | 'unknown'
export type ResultStatus = 'ok' | 'error' | 'unknown'

export type Tag = { label: string; level: Level }
export type Series = { name: string; values: (number | null)[] }

export type Block =
  | { type: 'text'; text: string; tone: Tone }
  | { type: 'tags'; tags: Tag[] }
  | { type: 'table'; columns: string[]; rows: string[][]; levels?: (Level | null)[][] }
  | { type: 'bar'; label: string; fraction: number; caption: string; level?: Level }
  | { type: 'sparkline'; label: string; values: number[]; unit: string }
  | { type: 'chart'; title: string; unit: string; timestamps: number[]; series: Series[] }

export type CommandResult = { command: string; blocks: Block[]; status: ResultStatus }

export type Tick = {
  wanInBps: number | null
  wanOutBps: number | null
  vpsUsedBytes: number | null
  vpsCapBytes: number
  streams: number | null
}
