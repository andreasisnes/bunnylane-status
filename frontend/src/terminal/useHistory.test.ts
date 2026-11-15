import { act, renderHook } from '@testing-library/react'
import { useHistory } from './useHistory'

beforeEach(() => localStorage.clear())

test('up and down walk the history newest first', () => {
  const { result } = renderHook(() => useHistory())
  act(() => result.current.push('status'))
  act(() => result.current.push('net 24h'))

  act(() => result.current.up())
  expect(result.current.current).toBe('net 24h')
  act(() => result.current.up())
  expect(result.current.current).toBe('status')
  act(() => result.current.down())
  expect(result.current.current).toBe('net 24h')
  act(() => result.current.down())
  expect(result.current.current).toBe('')
})

test('history persists across remount and is capped at 200 entries', () => {
  const first = renderHook(() => useHistory())
  act(() => {
    for (let i = 0; i < 205; i++) first.result.current.push(`cmd ${i}`)
  })
  first.unmount()

  const second = renderHook(() => useHistory())
  expect(second.result.current.entries).toHaveLength(200)
  expect(second.result.current.entries[199]).toBe('cmd 204')
  expect(JSON.parse(localStorage.getItem('bl.history')!)).toHaveLength(200)
})

test('consecutive duplicates are not stored twice', () => {
  const { result } = renderHook(() => useHistory())
  act(() => result.current.push('status'))
  act(() => result.current.push('status'))
  expect(result.current.entries).toEqual(['status'])
})
