/** The fixed command set. Mirrors the backend allow-list; adding a command is a new release of both halves. */
export type CommandSpec = { name: string; usage: string; args: string[]; local?: boolean }

export const COMMANDS: CommandSpec[] = [
  { name: 'help', usage: 'help', args: [] },
  { name: 'status', usage: 'status', args: [] },
  { name: 'nodes', usage: 'nodes', args: [] },
  { name: 'net', usage: 'net [1h|24h|7d|30d]', args: ['1h', '24h', '7d', '30d'] },
  { name: 'disks', usage: 'disks', args: [] },
  { name: 'nas', usage: 'nas', args: [] },
  { name: 'gpu', usage: 'gpu [1h|24h]', args: ['1h', '24h'] },
  { name: 'vps', usage: 'vps [month]', args: ['month'] },
  { name: 'top', usage: 'top [month|all]', args: ['month', 'all'] },
  { name: 'now', usage: 'now', args: [] },
  { name: 'watch', usage: 'watch <cmd>', args: [] },
  { name: 'clear', usage: 'clear', args: [], local: true },
  { name: 'history', usage: 'history', args: [], local: true },
  { name: 'theme', usage: 'theme [night|dawn]', args: ['night', 'dawn'], local: true },
]

/** Commands that make sense as chips on a phone. */
export const CHIPS = ['status', 'nodes', 'net', 'disks', 'nas', 'gpu', 'vps', 'top', 'now']
