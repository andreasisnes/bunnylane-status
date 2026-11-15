import { render, screen } from '@testing-library/react'
import { Header } from './Header'

test('the clock is 24-hour', () => {
  vi.useFakeTimers()
  vi.setSystemTime(new Date(2026, 10, 15, 15, 5, 0))
  render(<Header />)
  expect(screen.getByRole('time')).toHaveTextContent('15:05')
  expect(screen.getByRole('time')).not.toHaveTextContent(/PM/)
  vi.useRealTimers()
})
