import { act, render, screen } from '@testing-library/react'
import { Background } from './Background'
import { Ticker } from './Ticker'

class FakeEventSource {
  static last: FakeEventSource | null = null
  listeners: Record<string, ((e: MessageEvent) => void)[]> = {}
  closed = false
  url: string
  constructor(url: string) {
    this.url = url
    FakeEventSource.last = this
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

beforeEach(() => vi.stubGlobal('EventSource', FakeEventSource))

test('Ticker shows placeholders until a tick arrives, then the numbers', () => {
  const { unmount } = render(<Ticker />)
  expect(screen.getByText('wan', { exact: false })).toHaveTextContent('wan — / —')

  act(() => FakeEventSource.last!.emit('tick', { wanInBps: 184e6, wanOutBps: 20e6, vpsUsedBytes: 2.31e12, vpsCapBytes: 6e12, streams: 3 }))

  expect(screen.getByText('wan', { exact: false })).toHaveTextContent('wan ↓184 Mbit/s ↑20.0 Mbit/s')
  expect(screen.getByText('vps', { exact: false })).toHaveTextContent('vps 2.31 TB / 6.00 TB')
  expect(screen.getByText('3 streams')).toBeInTheDocument()
  unmount()
  expect(FakeEventSource.last!.closed).toBe(true)
})

test('Background has no starfield animation when the viewer prefers reduced motion', () => {
  vi.stubGlobal('matchMedia', (query: string) => ({ matches: query.includes('reduce'), addEventListener() {}, removeEventListener() {} }))
  render(<Background />)
  expect(screen.getByTestId('background')).not.toContainElement(screen.queryByTestId('starfield'))
})

test('Background draws the starfield when motion is allowed', () => {
  vi.stubGlobal('matchMedia', () => ({ matches: false, addEventListener() {}, removeEventListener() {} }))
  render(<Background />)
  expect(screen.getByTestId('starfield')).toBeInTheDocument()
})
