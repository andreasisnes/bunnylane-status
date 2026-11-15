import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import type { CommandResult } from '../lib/types'

export const statusResult: CommandResult = {
  command: 'status',
  status: 'ok',
  blocks: [
    {
      type: 'tags',
      tags: [
        { label: 'nodes 6/6', level: 'ok' },
        { label: '2 workloads', level: 'degraded' },
      ],
    },
  ],
}

export const helpResult: CommandResult = {
  command: 'help',
  status: 'ok',
  blocks: [{ type: 'table', columns: ['command', 'output'], rows: [['help', 'this table'], ['status', 'one line per area'], ['net [1h|24h|7d|30d]', 'wan chart']] }],
}

export const handlers = [
  http.post('*/api/command', async ({ request }) => {
    const { line } = (await request.json()) as { line: string }
    if (line === 'status') return HttpResponse.json(statusResult)
    if (line === 'help') return HttpResponse.json(helpResult)
    return HttpResponse.json({
      command: line,
      status: 'error',
      blocks: [{ type: 'text', text: `command not found: ${line} · try help`, tone: 'red' }],
    } satisfies CommandResult)
  }),
]

export const server = setupServer(...handlers)
