import { Background } from './chrome/Background'
import { Header } from './chrome/Header'
import { Terminal } from './terminal/Terminal'

export default function App() {
  return (
    <>
      <Background />
      <main className="app">
        <div className="panel">
          <Header />
          <Terminal />
        </div>
      </main>
    </>
  )
}
