import type { Tag } from '../lib/types'

export function TagsBlock({ tags }: { tags: Tag[] }) {
  return (
    <ul className="tags">
      {tags.map((t, i) => (
        <li key={`${t.label}-${i}`} className={`tag tag-${t.level}`}>
          <span className="tag-level">[{t.level.toUpperCase()}]</span> <span className="tag-label">{t.label}</span>
        </li>
      ))}
    </ul>
  )
}
