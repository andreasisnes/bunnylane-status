import type { Block } from '../lib/types'

/** Dispatches one block to its renderer. Task 11 replaces these bodies with the real components. */
export function BlockBody({ block }: { block: Block }) {
  switch (block.type) {
    case 'text':
      return <p className={`text tone-${block.tone}`}>{block.text}</p>
    case 'tags':
      return (
        <ul className="tags">
          {block.tags.map((t) => (
            <li key={t.label} className={`tag tag-${t.level}`}>
              <span className="tag-level">[{t.level.toUpperCase()}]</span> {t.label}
            </li>
          ))}
        </ul>
      )
    case 'table':
      return (
        <table className="table">
          <thead>
            <tr>
              {block.columns.map((c) => (
                <th key={c}>{c}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {block.rows.map((row, i) => (
              <tr key={i}>
                {row.map((cell, j) => (
                  <td key={j}>{cell}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      )
    case 'bar':
      return (
        <div className="bar">
          <span className="bar-label">{block.label}</span> {block.caption}
        </div>
      )
    case 'sparkline':
      return <div className="sparkline">{block.label}</div>
    case 'chart':
      return <div className="chart">{block.title}</div>
  }
}
