import { expect, test, type Page } from '@playwright/test'

const statusResult = {
  command: 'status',
  status: 'ok',
  blocks: [{ type: 'tags', tags: [{ label: 'nodes 6/6', level: 'ok' }, { label: 'storage', level: 'ok' }] }],
}
const helpResult = { command: 'help', status: 'ok', blocks: [{ type: 'table', columns: ['command', 'output'], rows: [['help', 'this table']] }] }
const netResult = { command: 'net', status: 'ok', blocks: [{ type: 'text', text: 'wan in 184 Mbit/s · out 20.0 Mbit/s', tone: 'amber' }] }

async function mockApi(page: Page) {
  await page.route('**/api/command', async (route) => {
    const { line } = route.request().postDataJSON() as { line: string }
    const body = line === 'status' ? statusResult : line === 'help' ? helpResult : line === 'net' ? netResult : {
      command: line, status: 'error', blocks: [{ type: 'text', text: `command not found: ${line} · try help`, tone: 'red' }],
    }
    await route.fulfill({ json: body })
  })
  await page.route('**/api/watch**', (route) => {
    const line = new URL(route.request().url()).searchParams.get('line') ?? ''
    const body = line.startsWith('status') ? statusResult : netResult
    return route.fulfill({
      status: 200,
      headers: { 'content-type': 'text/event-stream', 'cache-control': 'no-cache' },
      body: `event: result\ndata: ${JSON.stringify(body)}\n\nretry: 60000\n\n`,
    })
  })
  await page.route('**/api/ticker', (route) =>
    route.fulfill({
      status: 200,
      headers: { 'content-type': 'text/event-stream' },
      body: `event: tick\ndata: ${JSON.stringify({ wanInBps: 184e6, wanOutBps: 20e6, vpsUsedBytes: 2.31e12, vpsCapBytes: 6e12, streams: 1 })}\n\nretry: 60000\n\n`,
    }),
  )
}

test.beforeEach(async ({ page }) => {
  await mockApi(page)
  await page.goto('/')
  await expect(page.getByText('this table')).toBeVisible()
})

test('typing status shows an [OK] tag and the ticker fills in', async ({ page }) => {
  await page.getByRole('textbox', { name: 'command' }).fill('status')
  await page.keyboard.press('Enter')

  const block = page.getByRole('article', { name: 'status' })
  await expect(block).toContainText('[OK] nodes 6/6')
  await expect(page.getByText('vps 2.31 TB / 6.00 TB')).toBeVisible()
})

test('the phone shows command chips and tapping status runs it', async ({ page, isMobile }) => {
  test.skip(!isMobile, 'phone only')
  const chips = page.getByRole('group', { name: 'quick commands' })
  await expect(chips.getByRole('button', { name: 'status' })).toBeVisible()

  await chips.getByRole('button', { name: 'status' }).tap()

  await expect(page.getByRole('article', { name: 'status' })).toContainText('[OK] nodes 6/6')
})

test('watch net streams and Ctrl+C leaves a stopped block', async ({ page, isMobile }) => {
  test.skip(isMobile, 'desktop only')
  const input = page.getByRole('textbox', { name: 'command' })
  await input.fill('watch net')
  await page.keyboard.press('Enter')
  await expect(page.getByRole('article', { name: 'net' })).toContainText('wan in 184 Mbit/s')

  await input.press('Control+c')

  await expect(page.getByRole('article', { name: 'net (stopped)' })).toBeVisible()
})
