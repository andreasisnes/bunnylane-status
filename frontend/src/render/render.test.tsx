import { render, screen } from '@testing-library/react'
import { BarBlock } from './BarBlock'
import { BlockBody } from './Blocks'
import { ChartBlock } from './ChartBlock'
import { SparklineBlock } from './SparklineBlock'
import { TableBlock } from './TableBlock'
import { TagsBlock } from './TagsBlock'
import { TextBlock } from './TextBlock'

test('TagsBlock renders the level in brackets with a level class', () => {
  render(<TagsBlock tags={[{ label: '2 workloads', level: 'degraded' }, { label: 'edge', level: 'ok' }]} />)
  const degraded = screen.getByText('2 workloads', { exact: false }).closest('li')!
  expect(degraded).toHaveClass('tag-degraded')
  expect(degraded).toHaveTextContent('[DEGRADED] 2 workloads')
  expect(screen.getByText('edge', { exact: false }).closest('li')).toHaveClass('tag-ok')
})

test('BarBlock fills the fraction as a width and exposes it as a meter', () => {
  render(<BarBlock label="vps" fraction={0.385} caption="2.31 TB of 6 TB" />)
  const meter = screen.getByRole('meter', { name: 'vps' })
  expect(meter).toHaveAttribute('aria-valuenow', '38.5')
  expect(meter.querySelector('.bar-fill')).toHaveStyle({ width: '38.5%' })
  expect(screen.getByText('2.31 TB of 6 TB')).toBeInTheDocument()
})

test('TableBlock renders headers and rows as a table', () => {
  render(<TableBlock columns={['#', 'who']} rows={[['1', 'anna'], ['2', 'bo']]} />)
  expect(screen.getByRole('table')).toBeInTheDocument()
  expect(screen.getAllByRole('columnheader').map((h) => h.textContent)).toEqual(['#', 'who'])
  expect(screen.getAllByRole('row')).toHaveLength(3)
})

test('TextBlock carries its tone as a class', () => {
  render(<TextBlock text="wan in 184 Mbit/s" tone="amber" />)
  expect(screen.getByText('wan in 184 Mbit/s')).toHaveClass('tone-amber')
})

test('SparklineBlock draws one glyph per value and names the series', () => {
  render(<SparklineBlock label="GB per day" values={[0, 1, 2, 4, 8]} unit="GB" />)
  const spark = screen.getByLabelText('GB per day')
  expect(spark.textContent).toHaveLength(5)
  expect(spark.textContent!.endsWith('█')).toBe(true)
})

test('ChartBlock renders a figure with the title and a text summary when canvas is unavailable', () => {
  render(
    <ChartBlock
      title="net 1h"
      unit="bit/s"
      timestamps={[1700000000, 1700000060]}
      series={[{ name: 'in', values: [1_000_000, 2_000_000] }, { name: 'out', values: [null, 500_000] }]}
    />,
  )
  const figure = screen.getByRole('figure', { name: 'net 1h' })
  expect(figure).toHaveTextContent('in')
  expect(figure).toHaveTextContent('out')
  expect(figure).toHaveTextContent('2.00 Mbit/s')
})

test.each([
  ['ok', 0.4, 'bar-ok'],
  ['degraded', 0.7, 'bar-warn'],
  ['down', 0.85, 'bar-hot'],
] as const)('a %s bar gets the %s class', (level, fraction, cls) => {
  render(<BarBlock label="x" fraction={fraction} caption="c" level={level} />)
  expect(screen.getByRole('meter', { name: 'x' }).querySelector('.bar-fill')).toHaveClass(cls)
})

test('a bar with no level renders neutral, not hot', () => {
  render(<BarBlock label="x" fraction={0.95} caption="c" />)
  const fill = screen.getByRole('meter', { name: 'x' }).querySelector('.bar-fill')!
  expect(fill).not.toHaveClass('bar-hot')
  expect(fill).not.toHaveClass('bar-ok')
  expect(fill).not.toHaveClass('bar-warn')
})

test('BlockBody passes a bar level through to the renderer', () => {
  render(<BlockBody block={{ type: 'bar', label: 'nas', fraction: 0.85, caption: 'c', level: 'down' }} />)
  expect(screen.getByRole('meter', { name: 'nas' }).querySelector('.bar-fill')).toHaveClass('bar-hot')
})
