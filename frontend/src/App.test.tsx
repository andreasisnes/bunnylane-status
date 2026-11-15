import { render, screen } from '@testing-library/react'
import App from './App'

test('App renders the prompt placeholder', () => {
  render(<App />)
  expect(screen.getByPlaceholderText('type help')).toBeInTheDocument()
})
