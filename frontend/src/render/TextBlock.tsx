import type { Tone } from '../lib/types'

export function TextBlock({ text, tone }: { text: string; tone: Tone }) {
  return <p className={`text tone-${tone}`}>{text}</p>
}
