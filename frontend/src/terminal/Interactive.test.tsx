import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Terminal } from './Terminal'

class FakeEventSource {
  static CONNECTING = 0
  static OPEN = 1
  static CLOSED = 2
  static instances: FakeEventSource[] = []
  url: string
  readyState = 1
  closed = false
  listeners: Record<string, ((e: MessageEvent) => void)[]> = {}
  constructor(url: string) {
    this.url = url
    FakeEventSource.instances.push(this)
  }
  addEventListener(type: string, cb: (e: MessageEvent) => void) {
    ;(this.listeners[type] ??= []).push(cb)
  }
  emit(type: string, data: unknown) {
    this.listeners[type]?.forEach((cb) => cb(new MessageEvent(type, { data: JSON.stringify(data) })))
  }
  close() {
    this.closed = true
  }
}

const live = () => FakeEventSource.instances.filter((s) => !s.closed)

beforeEach(() => {
  FakeEventSource.instances = []
  vi.stubGlobal('EventSource', FakeEventSource)
  localStorage.clear()
})

test('clicking a command in the help table starts a live block', async () => {
  const user = userEvent.setup()
  render(<Terminal />)
  const help = await screen.findByRole('article', { name: 'help' })

  await user.click(within(help).getByRole('button', { name: 'status' }))

  await waitFor(() => expect(live()).toHaveLength(1))
  expect(live()[0].url).toContain('line=status')
  live()[0].emit('result', { command: 'status', status: 'ok', blocks: [{ type: 'tags', tags: [{ label: 'nodes 6/6', level: 'ok' }] }] })
  const block = await screen.findByRole('article', { name: 'status' })
  expect(block).toHaveTextContent('nodes 6/6')
  expect(within(block).getByText('live')).toBeInTheDocument()
})

test('a chip starts a live block and a second chip replaces it', async () => {
  const user = userEvent.setup()
  render(<Terminal />)
  await screen.findByRole('article', { name: 'help' })
  const chips = screen.getByRole('group', { name: 'quick commands' })

  await user.click(within(chips).getByRole('button', { name: 'net' }))
  await waitFor(() => expect(live()).toHaveLength(1))
  await user.click(within(chips).getByRole('button', { name: 'vps' }))

  await waitFor(() => expect(live()).toHaveLength(1))
  expect(live()[0].url).toContain('line=vps')
  expect(FakeEventSource.instances[0].closed).toBe(true)
  expect(await screen.findByRole('article', { name: 'net (stopped)' })).toBeInTheDocument()
})

test('argument chips on a live block switch the stream, stop ends it, close removes it', async () => {
  const user = userEvent.setup()
  render(<Terminal />)
  await screen.findByRole('article', { name: 'help' })
  await user.click(within(screen.getByRole('group', { name: 'quick commands' })).getByRole('button', { name: 'net' }))
  await waitFor(() => expect(live()).toHaveLength(1))
  const block = screen.getByRole('article', { name: 'net' })

  await user.click(within(block).getByRole('button', { name: '24h' }))
  await waitFor(() => expect(live()[0].url).toContain('line=net+24h'))
  expect(screen.getByRole('article', { name: 'net 24h' })).toBeInTheDocument()

  await user.click(within(screen.getByRole('article', { name: 'net 24h' })).getByRole('button', { name: 'stop' }))
  await waitFor(() => expect(live()).toHaveLength(0))
  const stopped = screen.getByRole('article', { name: 'net 24h (stopped)' })

  await user.click(within(stopped).getByRole('button', { name: 'go live' }))
  await waitFor(() => expect(live()).toHaveLength(1))

  await user.click(within(screen.getByRole('article', { name: 'net 24h' })).getByRole('button', { name: 'close' }))
  expect(screen.queryByRole('article', { name: 'net 24h' })).not.toBeInTheDocument()
  expect(live()).toHaveLength(0)
})

test('a typed command runs once and can be made live from its header', async () => {
  const user = userEvent.setup()
  render(<Terminal />)
  await screen.findByRole('article', { name: 'help' })

  await user.type(screen.getByRole('textbox', { name: 'command' }), 'status{Enter}')
  const block = await screen.findByRole('article', { name: 'status' })
  await waitFor(() => expect(block).toHaveTextContent('nodes 6/6'))
  expect(live()).toHaveLength(0)

  await user.click(within(block).getByRole('button', { name: 'go live' }))

  await waitFor(() => expect(live()).toHaveLength(1))
  expect(live()[0].url).toContain('line=status')
})

test('block times are 24-hour', async () => {
  vi.useFakeTimers({ shouldAdvanceTime: true })
  vi.setSystemTime(new Date(2026, 10, 15, 21, 7, 9))
  render(<Terminal />)
  const help = await screen.findByRole('article', { name: 'help' })
  expect(within(help).getByRole('time')).toHaveTextContent('21:07:09')
  vi.useRealTimers()
})
