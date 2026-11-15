import { downsample, MAX_GLYPHS } from '../lib/downsample'

test('long series are averaged into at most MAX_GLYPHS buckets', () => {
  const v = Array.from({ length: 360 }, (_, i) => i)
  const d = downsample(v)
  expect(d).toHaveLength(MAX_GLYPHS)
  expect(d[0]).toBeLessThan(d[MAX_GLYPHS - 1])
  expect(downsample([1, 2, 3])).toEqual([1, 2, 3])
})
