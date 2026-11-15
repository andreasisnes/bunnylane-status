import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Terminal } from './Terminal'

class FakeEventSource {
  static CONNECTING = 0
  static OPEN = 1
  static CLOSED = 2
  static instances: FakeEventSource[] = []
  url: string
  readyState = 0
  closed = false
  listeners: Record<string, ((e: MessageEvent) => void)[]> = {}
  onerror: ((e: Event) => void) | null = null
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
  drop() {
    this.listeners['error']?.forEach((cb) => cb(new Event('error') as MessageEvent))
  }
  close() {
    this.closed = true
  }
}

beforeEach(() => {
  FakeEventSource.instances = []
  vi.stubGlobal('EventSource', FakeEventSource)
  localStorage.clear()
})

test('first visit runs help', async () => {
  render(<Terminal />)
  expect(await screen.findByText('this table')).toBeInTheDocument()
})

test('typing status and Enter renders a block with the header and the tags', async () => {
  const user = userEvent.setup()
  render(<Terminal />)
  await screen.findByText('this table')

  await user.type(screen.getByRole('textbox', { name: 'command' }), 'status{Enter}')

  const block = await screen.findByRole('article', { name: 'status' })
  expect(block).toHaveTextContent('nodes 6/6')
  expect(block).toHaveTextContent('2 workloads')
  expect(screen.getByRole('textbox', { name: 'command' })).toHaveValue('')
})

test('unknown command shows the not-found text', async () => {
  const user = userEvent.setup()
  render(<Terminal />)
  await screen.findByText('this table')

  await user.type(screen.getByRole('textbox', { name: 'command' }), 'reboot{Enter}')

  expect(await screen.findByText(/command not found: reboot/)).toBeInTheDocument()
})

test('watch opens a stream and Ctrl+C closes it', async () => {
  const user = userEvent.setup()
  render(<Terminal />)
  await screen.findByText('this table')
  const input = screen.getByRole('textbox', { name: 'command' })

  await user.type(input, 'watch net{Enter}')
  await waitFor(() => expect(FakeEventSource.instances).toHaveLength(1))
  expect(FakeEventSource.instances[0].url).toContain('/api/watch?line=net')
  FakeEventSource.instances[0].emit('result', { command: 'net', status: 'ok', blocks: [{ type: 'text', text: 'wan in 184 Mbit/s', tone: 'amber' }] })
  expect(await screen.findByText('wan in 184 Mbit/s')).toBeInTheDocument()

  await user.keyboard('{Control>}c{/Control}')

  expect(FakeEventSource.instances[0].closed).toBe(true)
  expect(await screen.findByRole('article', { name: 'net (stopped)' })).toBeInTheDocument()
})

test('Tab completes and Ctrl+L clears', async () => {
  const user = userEvent.setup()
  render(<Terminal />)
  await screen.findByText('this table')
  const input = screen.getByRole('textbox', { name: 'command' })

  await user.type(input, 'sta')
  await user.keyboard('{Tab}')
  expect(input).toHaveValue('status')

  await user.keyboard('{Control>}l{/Control}')
  expect(screen.queryByText('this table')).not.toBeInTheDocument()
})

test('a watch whose connection drops is marked stopped and the prompt is free again', async () => {
  const user = userEvent.setup()
  render(<Terminal />)
  await screen.findByText('this table')
  const input = screen.getByRole('textbox', { name: 'command' })

  await user.type(input, 'watch net{Enter}')
  await waitFor(() => expect(FakeEventSource.instances).toHaveLength(1))
  expect(input).toHaveAttribute('placeholder', expect.stringMatching(/running/))

  FakeEventSource.instances[0].drop()

  expect(await screen.findByRole('article', { name: 'net (stopped)' })).toHaveTextContent(/connection/i)
  expect(input).toHaveAttribute('placeholder', 'type help')
})
