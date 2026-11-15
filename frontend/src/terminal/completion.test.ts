import { complete } from './completion'
import { COMMANDS } from '../lib/commands'

test('completes command names by prefix, sorted', () => {
  expect(complete('n', COMMANDS)).toEqual(['nas', 'net', 'nodes', 'now'])
})

test('completes an argument of a known command', () => {
  expect(complete('net 2', COMMANDS)).toEqual(['24h'])
  expect(complete('net ', COMMANDS)).toEqual(['1h', '24h', '7d', '30d'])
})

test('completes the target of watch but never watch itself', () => {
  expect(complete('watch n', COMMANDS)).toEqual(['nas', 'net', 'nodes', 'now'])
  expect(complete('watch w', COMMANDS)).toEqual([])
})

test('returns nothing for unknown input or commands without arguments', () => {
  expect(complete('xyz', COMMANDS)).toEqual([])
  expect(complete('status ', COMMANDS)).toEqual([])
})
