import { chromium } from '@playwright/test'

const ts = Array.from({ length: 120 }, (_, i) => 1700000000 + i * 30)
const netResult = {
  command: 'net', status: 'ok',
  blocks: [
    { type: 'text', text: 'wan in 184 Mbit/s · out 20.0 Mbit/s', tone: 'amber' },
    { type: 'sparkline', label: 'in, last 1h', values: ts.map((_, i) => 100 + Math.sin(i / 7) * 50), unit: 'bit/s' },
    { type: 'chart', title: 'net 1h', unit: 'bit/s', timestamps: ts, series: [
      { name: 'in', values: ts.map((_, i) => 1e8 + Math.sin(i / 7) * 5e7) },
      { name: 'out', values: ts.map((_, i) => 2e7 + Math.cos(i / 9) * 1e7) } ] },
    { type: 'table', columns: ['node', 'in', 'out'], rows: [['w-1', '1.00 Mbit/s', '2.00 Mbit/s'], ['w-2', '1.00 Mbit/s', '2.00 Mbit/s']] },
  ],
}
const browser = await chromium.launch()
for (const [name, viewport] of [['desktop', { width: 1280, height: 900 }], ['phone', { width: 390, height: 844 }]] as const) {
  const page = await browser.newPage({ viewport })
  await page.route('**/api/command', (r) => r.fulfill({ json: netResult }))
  await page.route('**/api/ticker', (r) => r.fulfill({ status: 200, headers: { 'content-type': 'text/event-stream' }, body: 'event: tick\ndata: {"wanInBps":1,"wanOutBps":1,"vpsUsedBytes":1,"vpsCapBytes":6e12,"streams":0}\n\n' }))
  await page.goto('http://127.0.0.1:4173/')
  await page.getByRole('textbox', { name: 'command' }).fill('net')
  await page.keyboard.press('Enter')
  await page.getByRole('article', { name: 'net' }).waitFor()
  await page.waitForTimeout(900)
  const m = await page.evaluate(() => {
    const block = document.querySelector('.block:last-of-type') as HTMLElement
    const canvas = document.querySelector('.chart canvas') as HTMLCanvasElement | null
    const panel = document.querySelector('.panel') as HTMLElement
    const yTicks = Array.from(document.querySelectorAll('.u-axis')).slice(-1).map((a) => a.textContent)
    const xTicks = Array.from(document.querySelectorAll('.u-axis')).slice(-2, -1).map((a) => a.textContent)
    return { yTicks, xTicks, blockScroll: block.scrollWidth, blockClient: block.clientWidth, canvasW: canvas?.getBoundingClientRect().width ?? -1, panelW: panel.clientWidth, docScroll: document.documentElement.scrollWidth, docClient: document.documentElement.clientWidth }
  })
  console.log(name, JSON.stringify(m), m.blockScroll <= m.blockClient && m.docScroll <= m.docClient ? 'OK no overflow' : 'OVERFLOW')
  await page.screenshot({ path: `test-results/net-${name}.png`, fullPage: false })
  await page.close()
}
await browser.close()
