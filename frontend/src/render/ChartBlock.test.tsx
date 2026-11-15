import { render } from '@testing-library/react'
import { ChartBlock } from './ChartBlock'

const construct = vi.fn()
const setData = vi.fn()
const destroy = vi.fn()
vi.mock('uplot', () => ({
  default: class {
    constructor(...args: unknown[]) {
      construct(...args)
    }
    setData = setData
    setSize = vi.fn()
    destroy = destroy
  },
}))

beforeEach(() => {
  construct.mockClear()
  setData.mockClear()
  destroy.mockClear()
  vi.stubGlobal('ResizeObserver', class { observe() {} disconnect() {} })
  HTMLCanvasElement.prototype.getContext = vi.fn(() => ({}) as unknown as CanvasRenderingContext2D) as never
  vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) => { cb(performance.now() + 1000); return 1 })
  vi.stubGlobal('cancelAnimationFrame', () => {})
})

const props = { title: 'net 1h', unit: 'bit/s', timestamps: [1, 2, 3] }

test('a watch tick updates the existing plot instead of rebuilding it', () => {
  const { rerender } = render(<ChartBlock {...props} series={[{ name: 'in', values: [1, 2, 3] }]} />)
  expect(construct).toHaveBeenCalledTimes(1)

  rerender(<ChartBlock {...props} timestamps={[2, 3, 4]} series={[{ name: 'in', values: [2, 3, 4] }]} />)

  expect(construct).toHaveBeenCalledTimes(1)
  expect(destroy).not.toHaveBeenCalled()
  expect(setData).toHaveBeenLastCalledWith([[2, 3, 4], [2, 3, 4]])
})

test('a different series shape rebuilds the plot', () => {
  const { rerender } = render(<ChartBlock {...props} series={[{ name: 'in', values: [1, 2, 3] }]} />)

  rerender(<ChartBlock {...props} series={[{ name: 'in', values: [1, 2, 3] }, { name: 'out', values: [0, 0, 0] }]} />)

  expect(destroy).toHaveBeenCalledTimes(1)
  expect(construct).toHaveBeenCalledTimes(2)
})
