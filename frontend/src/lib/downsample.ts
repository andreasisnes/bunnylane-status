/** Keep the glyph string short enough for a phone: average into at most MAX buckets. */
export const MAX_GLYPHS = 48

export function downsample(values: number[], max = MAX_GLYPHS): number[] {
  if (values.length <= max) return values
  const out: number[] = []
  const size = values.length / max
  for (let b = 0; b < max; b++) {
    const from = Math.floor(b * size)
    const to = Math.max(from + 1, Math.floor((b + 1) * size))
    const slice = values.slice(from, to)
    out.push(slice.reduce((a, v) => a + v, 0) / slice.length)
  }
  return out
}
