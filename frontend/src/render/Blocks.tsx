import type { Block } from '../lib/types'
import { BarBlock } from './BarBlock'
import { ChartBlock } from './ChartBlock'
import { SparklineBlock } from './SparklineBlock'
import { TableBlock } from './TableBlock'
import { TagsBlock } from './TagsBlock'
import { TextBlock } from './TextBlock'

/** Dispatches one server block to its renderer. The set is closed; anything else is ignored. */
export function BlockBody({ block }: { block: Block }) {
  switch (block.type) {
    case 'text':
      return <TextBlock text={block.text} tone={block.tone} />
    case 'tags':
      return <TagsBlock tags={block.tags} />
    case 'table':
      return <TableBlock columns={block.columns} rows={block.rows} />
    case 'bar':
      return <BarBlock label={block.label} fraction={block.fraction} caption={block.caption} />
    case 'sparkline':
      return <SparklineBlock label={block.label} values={block.values} unit={block.unit} />
    case 'chart':
      return <ChartBlock title={block.title} unit={block.unit} timestamps={block.timestamps} series={block.series} />
    default:
      return null
  }
}
