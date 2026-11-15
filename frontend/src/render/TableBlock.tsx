type Props = {
  columns: string[]
  rows: string[][]
  /** When set, the first cell of each row is a button that fires with the row's first token (used by `help`). */
  onPick?: (command: string) => void
}

export function TableBlock({ columns, rows, onPick }: Props) {
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
                {row.map((cell, j) =>
                  j === 0 && onPick ? (
                    <td key={j}>
                      <button type="button" className="table-link" onClick={() => onPick(cell.split(' ')[0])}>
                        {cell}
                      </button>
                    </td>
                  ) : (
                    <td key={j}>{cell}</td>
                  ),
                )}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  )
}
