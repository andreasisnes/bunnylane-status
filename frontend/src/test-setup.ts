import '@testing-library/jest-dom/vitest'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { server } from './test/server'

// jsdom has no matchMedia; uPlot and the Background read it at load time.
if (typeof window !== 'undefined' && typeof window.matchMedia !== 'function') {
  window.matchMedia = (query: string) =>
    ({
      matches: false,
      media: query,
      onchange: null,
      addEventListener() {},
      removeEventListener() {},
      addListener() {},
      removeListener() {},
      dispatchEvent: () => false,
    }) as MediaQueryList
}

// jsdom has no EventSource; tests that care stub their own.
if (typeof globalThis.EventSource === 'undefined') {
  class NoopEventSource {
    static readonly CONNECTING = 0
    static readonly OPEN = 1
    static readonly CLOSED = 2
    readyState = 2
    constructor(public_url: string) {
      void public_url
    }
    addEventListener() {}
    removeEventListener() {}
    close() {}
  }
  globalThis.EventSource = NoopEventSource as unknown as typeof EventSource
}

beforeAll(() => server.listen({ onUnhandledRequest: 'bypass' } as Parameters<typeof server.listen>[0]))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())
