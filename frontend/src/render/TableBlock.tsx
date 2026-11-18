import type { Level } from '../lib/types'

type Props = {
  columns: string[]
  rows: string[][]
  /** When set, the first cell of each row is a button that fires with the row's first token (used by `help`). */
  onPick?: (command: string) => void
  /** Optional, parallel to `rows`: a band for cell [r][c]. Indexed defensively — short or ragged is fine. */
  levels?: (Level | null)[][]
}

/** The server owns the threshold; this only maps its band to a class. `unknown` stays neutral. */
const BAND: Partial<Record<Level, string>> = { ok: 'cell-ok', degraded: 'cell-warn', down: 'cell-hot' }

export function TableBlock({ columns, rows, onPick, levels }: Props) {
  return (
    <div className="table-wrap">
      <table className="table">
        <thead>
          <tr>
            {columns.map((c) => (
              <th key={c} scope="col">
                {c}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 ? (
            <tr>
              <td className="table-empty" colSpan={columns.length}>
                nothing yet
              </td>
            </tr>
          ) : (
            rows.map((row, i) => (
              <tr key={i}>
                {row.map((cell, j) => {
                  const level = levels?.[i]?.[j]
                  const band = (level && BAND[level]) || undefined
                  return j === 0 && onPick ? (
                    <td key={j} className={band}>
                      <button type="button" className="table-link" onClick={() => onPick(cell.split(' ')[0])}>
                        {cell}
                      </button>
                    </td>
                  ) : (
                    <td key={j} className={band}>
                      {cell}
                    </td>
                  )
                })}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  )
}
