import { render, screen } from '@testing-library/react'
import App from './App'

beforeEach(() => localStorage.clear())

test('App renders the prompt placeholder and runs help on first visit', async () => {
  render(<App />)
  expect(screen.getByPlaceholderText('type help')).toBeInTheDocument()
  expect(await screen.findByText('this table')).toBeInTheDocument()
})
