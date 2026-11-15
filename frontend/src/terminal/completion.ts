import type { CommandSpec } from '../lib/commands'

/** Prefix completion: the command name first, then its allowed argument. `watch` completes its target. */
export function complete(input: string, commands: CommandSpec[]): string[] {
  const tokens = input.toLowerCase().split(/\s+/)
  const trailingSpace = /\s$/.test(input)

  if (tokens[0] === 'watch' && (tokens.length > 1 || trailingSpace)) {
    const rest = input.replace(/^\s*watch\s*/i, '')
    return complete(rest, commands.filter((c) => c.name !== 'watch' && c.name !== 'help' && !c.local))
  }

  if (tokens.length === 1 && !trailingSpace) {
    return commands.map((c) => c.name).filter((n) => n.startsWith(tokens[0])).sort()
  }

  const spec = commands.find((c) => c.name === tokens[0])
  if (!spec || tokens.length > 2) return []
  const partial = tokens[1] ?? ''
  return spec.args.filter((a) => a.startsWith(partial))
}
