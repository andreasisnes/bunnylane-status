import { openWatch } from './api'

class FakeEventSource {
  static CONNECTING = 0
  static OPEN = 1
  static CLOSED = 2
  static last: FakeEventSource | null = null
  readyState = 0
  closed = false
  listeners: Record<string, ((e: Event) => void)[]> = {}
  url: string
  constructor(url: string) {
    this.url = url
    FakeEventSource.last = this
  }
  addEventListener(type: string, cb: (e: Event) => void) {
    ;(this.listeners[type] ??= []).push(cb)
  }
  fire(type: string, e: Event) {
    this.listeners[type]?.forEach((cb) => cb(e))
  }
  close() {
    this.closed = true
  }
}

beforeEach(() => vi.stubGlobal('EventSource', FakeEventSource))

test('a dropped connection reports an error instead of staying silent', () => {
  const onError = vi.fn()
  openWatch('net', vi.fn(), onError)
  const source = FakeEventSource.last!
  source.readyState = FakeEventSource.CONNECTING

  source.fire('error', new Event('error'))

  expect(onError).toHaveBeenCalledWith(expect.stringMatching(/connection/i))
  expect(source.closed).toBe(true)
})

test('a rejected line reports the server message', () => {
  const onError = vi.fn()
  openWatch('rm -rf', vi.fn(), onError)
  const source = FakeEventSource.last!

  source.fire('error', new MessageEvent('error', { data: JSON.stringify({ command: 'rm -rf', status: 'error', blocks: [{ type: 'text', text: 'command not found: rm -rf · try help', tone: 'red' }] }) }))

  expect(onError).toHaveBeenCalledWith('command not found: rm -rf · try help')
  expect(source.closed).toBe(true)
})
