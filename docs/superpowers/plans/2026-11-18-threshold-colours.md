# Threshold Colours Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Colour every utilisation percentage by the agreed three-band scale, on the wire and in the renderers.

**Architecture:** The server decides the band (the design's anti-goal keeps output pre-shaped server-side) and sends it as the existing `Level` enum. `BarBlock` gains an optional `Level`; `TableBlock` gains an optional `Levels` array parallel to `Rows`. The renderers map level → CSS class. Nothing computes a threshold in the browser.

**Tech Stack:** .NET 10 minimal API, xUnit; React 19 + TypeScript, Vitest + @testing-library/react, plain CSS custom properties.

**Spec:** `../../../../homelab/docs/design/2026-11-17-status-dashboard-design.md` (committed as `a55a31d` in the homelab repo) — see its **Colour thresholds** section.

**Plan 1 of 3.** This one ships on the *existing terminal*: bars and tables get colour, nothing is deleted, the page keeps working. Plan 2 adds the snapshot, `GET /api/snapshot` and the SignalR hub alongside the terminal. Plan 3 builds the dashboard frontend and only then deletes the terminal and the old endpoints.

## Global Constraints

- Bands, verbatim from the spec: `< 60%` → `Level.Ok` (`--bl-moon`), `60%–80%` → `Level.Degraded` (`--bl-amber`), `> 80%` → `Level.Down` (`--bl-red`).
- Lower bounds inclusive: 60.0% is Degraded, 80.0% is Degraded, 80.1% is Down.
- **No new `Level` value and no new palette token.** `Level` stays `Ok | Degraded | Down | Unknown`; the three colours already exist in both themes.
- Applies only to a fraction of a capacity: node CPU %, node memory %, GPU busy %, GPU VRAM %, Longhorn used %, NAS used %, VPS month-to-date. **Never** to load average, uptime, bitrates, stream counts or watch time.
- `.tag-ok` / `.tag-degraded` / `.tag-down` keep their current colours. This scale governs utilisation numbers and bars, not health tags.
- Both additions are **optional** on the wire, so a payload without them still renders.
- All frontend commands run from `frontend/`; backend tests via `dotnet test backend`.

## Review Focus

Input classes the spec implies that the happy-path tests would miss:

1. **A fraction outside 0..1 or NaN.** `Format.Percent` already clamps; `LevelFor` must agree and must not throw or return a wrong band for NaN. *Covered in Task 1, Step 1.*
2. **Exact boundaries 0.6 and 0.8.** Off-by-one here silently mis-colours the most common readings. *Covered in Task 1, Step 1.*
3. **A `bar` with no `level`** — every block produced by code this plan does not touch, which is most of them while the terminal still ships. Must render in the neutral colour, not unstyled or crashed. *Covered in Task 3, Step 1.*
4. **A ragged or short `levels` array** against `rows` — a producer bug must not blank the table or throw. *Covered in Task 4, Step 1.*
5. **The dawn theme.** All three bands must stay legible when `--bl-moon`/`--bl-muted` change. *Covered in Task 3, Step 7 and Task 4, Step 5.*

---

### Task 1: Backend threshold helper and wire types

**Files:**
- Create: `backend/BunnyLane.Status/Render/Thresholds.cs`
- Modify: `backend/BunnyLane.Status/Render/Blocks.cs` (the `BarBlock` and `TableBlock` records)
- Test: `backend/BunnyLane.Status.Tests/Render/ThresholdsTests.cs`, and extend `backend/BunnyLane.Status.Tests/Render/BlocksTests.cs`

**Interfaces:**
- Consumes: the existing `Level` enum from `Render/Blocks.cs`.
- Produces: `Thresholds.LevelFor(double fraction) -> Level`; `BarBlock(string Label, double Fraction, string Caption, Level? Level = null)`; `TableBlock(string[] Columns, string[][] Rows, Level?[][]? Levels = null)`. Task 2 calls all three.

- [ ] **Step 1: Write the failing tests**

In `ThresholdsTests.cs` — the bands, both boundaries, and the out-of-range cases from Review Focus 1 and 2:

```csharp
[Theory]
[InlineData(0.0, Level.Ok)]
[InlineData(0.599, Level.Ok)]
[InlineData(0.6, Level.Degraded)]     // lower bound inclusive
[InlineData(0.8, Level.Degraded)]     // upper bound inclusive
[InlineData(0.801, Level.Down)]
[InlineData(1.0, Level.Down)]
[InlineData(-0.5, Level.Ok)]          // clamped, matches Format.Percent
[InlineData(1.5, Level.Down)]         // clamped
public void LevelFor_maps_a_fraction_to_a_band(double fraction, Level expected) =>
    Assert.Equal(expected, Thresholds.LevelFor(fraction));

[Fact]
public void LevelFor_treats_NaN_as_unknown() =>
    Assert.Equal(Level.Unknown, Thresholds.LevelFor(double.NaN));
```

In `BlocksTests.cs` — that both additions are omitted from JSON when null, so existing payloads are byte-identical:

```csharp
[Fact]
public void A_bar_without_a_level_serialises_without_the_property()
{
    var json = JsonSerializer.Serialize<Block>(new BarBlock("nas", 0.39, "2.34 / 6.00 TB"));
    Assert.DoesNotContain("level", json);
}

[Fact]
public void A_bar_with_a_level_serialises_it_lower_case()
{
    var json = JsonSerializer.Serialize<Block>(new BarBlock("nas", 0.85, "x", Level.Down));
    Assert.Contains("\"level\":\"down\"", json);
}

[Fact]
public void A_table_without_levels_serialises_without_the_property()
{
    var json = JsonSerializer.Serialize<Block>(new TableBlock(["a"], [["1"]]));
    Assert.DoesNotContain("levels", json);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend --filter "ThresholdsTests|BlocksTests"`
Expected: FAIL — `Thresholds` does not exist; the `BarBlock`/`TableBlock` constructors do not take the new arguments.

- [ ] **Step 3: Implement**

`Render/Thresholds.cs`: `public static class Thresholds` with `public static Level LevelFor(double fraction)`. NaN returns `Level.Unknown`; otherwise clamp to 0..1 and apply the bands from Global Constraints.

`Render/Blocks.cs`: add the trailing optional parameters to both records exactly as given in Interfaces. Both need `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` on the new members so null is omitted.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend --filter "ThresholdsTests|BlocksTests"`
Expected: PASS.

- [ ] **Step 5: Run the whole backend suite**

Run: `dotnet test backend`
Expected: PASS. The new parameters are optional, so no existing call site changes.

- [ ] **Step 6: Commit**

```bash
git add backend/BunnyLane.Status/Render/Thresholds.cs backend/BunnyLane.Status/Render/Blocks.cs backend/BunnyLane.Status.Tests/Render/
git commit -m "feat(render): threshold bands on bar and table blocks"
```

---

### Task 2: Producers populate the levels

**Files:**
- Modify: `backend/BunnyLane.Status/Commands/Handlers/NodesCommand.cs`, `DisksCommand.cs`, `NasCommand.cs`, `GpuCommand.cs`, `VpsCommand.cs`
- Test: extend the existing tests for those handlers in `backend/BunnyLane.Status.Tests/`

**Interfaces:**
- Consumes: `Thresholds.LevelFor`, and the new `BarBlock.Level` / `TableBlock.Levels` from Task 1.
- Produces: nothing new. Plan 3's snapshot producers reuse `Thresholds.LevelFor` directly, not these handlers.

- [ ] **Step 1: Write the failing tests**

One test per handler, asserting the level travels with the value. For `nodes`, the table's `Levels` must be non-null only in the cpu and memory columns (indices 2 and 3) and null for node, role, load and uptime — Global Constraints forbid colouring load and uptime:

The fake is the existing `FakePrometheus` (`backend/BunnyLane.Status.Tests/Commands/Handlers/FakePrometheus.cs`), built with `.Set(promql, value)` and `.SetRange(promql, series)`. Follow how `NodesCommandTests` already seeds `Promql.NodeRole` / `NodeNames` to get an alias map, then add the cpu and memory values:

```csharp
[Fact]
public async Task Nodes_colours_only_the_cpu_and_memory_cells()
{
    var prom = NodesFixture()                       // the existing helper that seeds NodeRole + NodeNames
        .Set(Promql.NodeCpu, I(0.63, instance: "w1:9100"))
        .Set(Promql.NodeMem, I(0.86, instance: "w1:9100"));

    var table = Assert.IsType<TableBlock>((await new NodesCommand(prom).RunAsync([], default)).Blocks[0]);

    Assert.Equal(Level.Degraded, table.Levels![0][2]);   // cpu 63%
    Assert.Equal(Level.Down,     table.Levels![0][3]);   // mem 86%
    Assert.Null(table.Levels![0][0]);                    // node alias
    Assert.Null(table.Levels![0][4]);                    // load is not a capacity
    Assert.Null(table.Levels![0][5]);                    // uptime is not a capacity
}
```

Mirror that shape for `disks`, `nas`, `gpu` and `vps`, each asserting the `BarBlock.Level` its percentage implies.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend --filter "NodesCommand|DisksCommand|NasCommand|GpuCommand|VpsCommand"`
Expected: FAIL — `Levels` is null and `BarBlock.Level` is null.

- [ ] **Step 3: Implement**

In each handler, pass `Thresholds.LevelFor(fraction)` for the percentage it already computes. `NodesCommand` builds a `Level?[][]` the same shape as its `rows`, with entries only at the cpu and memory indices. GPU sets a level for busy and for VRAM used/size. VPS sets it for month-to-date over `VpsCapBytes`.

Do not touch `NowCommand`, `TopCommand`, `StatusCommand` or `HelpCommand` — none of them renders a fraction of a capacity.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend --filter "NodesCommand|DisksCommand|NasCommand|GpuCommand|VpsCommand"`
Expected: PASS.

- [ ] **Step 5: Run the whole backend suite**

Run: `dotnet test backend`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/
git commit -m "feat(status): send threshold levels with every utilisation percentage"
```

---

### Task 3: BarBlock renders the level

**Files:**
- Modify: `frontend/src/lib/types.ts` (the `bar` variant), `frontend/src/render/BarBlock.tsx`, `frontend/src/render/Blocks.tsx` (`BlockBody` must pass the new prop through — it currently does not), `frontend/src/theme.css`
- Test: `frontend/src/render/render.test.tsx` (extend; it will need to import `BlockBody` from `./Blocks`)

**Interfaces:**
- Consumes: `level?: Level` on the `bar` block, from Task 1's wire change.
- Produces: the CSS classes `bar-ok`, `bar-warn`, `bar-hot` on `.bar-fill`. Task 4 reuses the same `--bl-*` tokens for cells.

- [ ] **Step 1: Write the failing tests**

Covers the three bands plus Review Focus 3 — a bar with no level:

`render.test.tsx` renders each component directly — there is no wrapper component with a `blocks` prop. Match that style:

```tsx
test.each([
  ['ok', 0.4, 'bar-ok'],
  ['degraded', 0.7, 'bar-warn'],
  ['down', 0.9, 'bar-hot'],
] as const)('a %s bar gets the %s class', (level, fraction, cls) => {
  render(<BarBlock label="x" fraction={fraction} caption="c" level={level} />)
  expect(screen.getByRole('meter', { name: 'x' }).querySelector('.bar-fill')).toHaveClass(cls)
})

test('a bar with no level renders neutral, not hot', () => {
  render(<BarBlock label="x" fraction={0.95} caption="c" />)
  const fill = screen.getByRole('meter', { name: 'x' }).querySelector('.bar-fill')!
  expect(fill).not.toHaveClass('bar-hot')
  expect(fill).not.toHaveClass('bar-ok')
})

test('BlockBody passes a bar level through to the renderer', () => {
  render(<BlockBody block={{ type: 'bar', label: 'nas', fraction: 0.9, caption: 'c', level: 'down' }} />)
  expect(screen.getByRole('meter', { name: 'nas' }).querySelector('.bar-fill')).toHaveClass('bar-hot')
})
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `npx vitest run src/render/render.test.tsx`
Expected: FAIL — the classes are absent, and the no-level case currently gets `bar-hot` because of the `pct >= 90` rule.

- [ ] **Step 3: Add `level` to the `bar` block type**

In `lib/types.ts`, the `bar` variant becomes `{ type: 'bar'; label: string; fraction: number; caption: string; level?: Level }`.

- [ ] **Step 4: Implement the renderer**

In `BarBlock.tsx`, accept `level?: Level` and map it to one class: `ok → bar-ok`, `degraded → bar-warn`, `down → bar-hot`, `unknown` or absent → none. **Delete the `pct >= 90` rule** — the server owns the threshold now. Keep the `role="meter"` and all four aria attributes exactly as they are; colour must not be the only channel.

In `Blocks.tsx`, `case 'bar'` currently reads `label`, `fraction` and `caption` only. Add `level={block.level}`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `npx vitest run src/render/render.test.tsx`
Expected: PASS.

- [ ] **Step 6: Add the CSS**

In `theme.css`, replace the single `.bar-fill.bar-hot` rule with `.bar-fill.bar-ok { background: var(--bl-moon) }`, `.bar-fill.bar-warn { background: var(--bl-amber) }`, `.bar-fill.bar-hot { background: var(--bl-red) }`. Leave the base `.bar-fill` amber so an unlevelled bar is unchanged. The `box-shadow` currently hardcodes `var(--bl-amber-glow)`; make it match the band or drop it, because an amber glow under a red fill reads as a rendering bug.

- [ ] **Step 7: Run the suite and the linter, then check both themes**

Run: `npm test && npm run lint`
Expected: all pass; oxlint clean.

Then `npm run dev` and, per Review Focus 5, run `disks` and `nas` in both `theme night` and `theme dawn`, confirming all three bar colours stay legible against `--bl-card`.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/lib/types.ts frontend/src/render/BarBlock.tsx frontend/src/render/Blocks.tsx frontend/src/theme.css frontend/src/render/render.test.tsx
git commit -m "feat(render): colour bars by the server's threshold band"
```

---

### Task 4: TableBlock renders the levels

**Files:**
- Modify: `frontend/src/lib/types.ts` (the `table` variant), `frontend/src/render/TableBlock.tsx`, `frontend/src/render/Blocks.tsx` (`BlockBody` must pass `levels` through), `frontend/src/theme.css`
- Test: `frontend/src/render/render.test.tsx` (extend)

**Interfaces:**
- Consumes: `levels?: (Level | null)[][]` on the `table` block, from Task 1.
- Produces: the CSS classes `cell-ok`, `cell-warn`, `cell-hot` on `td`.

- [ ] **Step 1: Write the failing tests**

The happy path, and Review Focus 4 — a `levels` array shorter than `rows`:

```tsx
test('a table colours only the cells with a level', () => {
  const { container } = render(
    <TableBlock columns={['node', 'cpu']} rows={[['w-1', '63%']]} levels={[[null, 'degraded']]} />,
  )
  const cells = container.querySelectorAll('td')
  expect(cells[0]).not.toHaveClass('cell-warn')
  expect(cells[1]).toHaveClass('cell-warn')
})

test('a short levels array leaves the remaining rows plain', () => {
  const { container } = render(
    <TableBlock columns={['node', 'cpu']} rows={[['w-1', '63%'], ['w-2', '12%']]} levels={[[null, 'degraded']]} />,
  )
  const bodyRows = container.querySelectorAll('tbody tr')
  expect(bodyRows).toHaveLength(2)
  expect(bodyRows[1].querySelectorAll('td')[1]).not.toHaveClass('cell-warn')
})
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `npx vitest run src/render/render.test.tsx`
Expected: FAIL — the classes are absent.

- [ ] **Step 3: Add `levels` to the `table` block type**

In `lib/types.ts`, the `table` variant gains `levels?: (Level | null)[][]`.

- [ ] **Step 4: Implement the renderer**

In `TableBlock.tsx`, read `levels?.[i]?.[j]` for each cell and add one class: `ok → cell-ok`, `degraded → cell-warn`, `down → cell-hot`, anything else → none. Index safely so a short or ragged array cannot throw. The existing `onPick` first-cell button behaviour must be preserved unchanged.

In `Blocks.tsx`, `case 'table'` must add `levels={block.levels}`.

- [ ] **Step 5: Run the tests, add the CSS, check both themes**

Run: `npx vitest run src/render/render.test.tsx`
Expected: PASS.

Then add `.cell-ok { color: var(--bl-moon) }`, `.cell-warn { color: var(--bl-amber) }`, `.cell-hot { color: var(--bl-red) }` to `theme.css`, and confirm in `npm run dev` that `nodes` reads correctly in both themes.

- [ ] **Step 6: Run the full suite and the linter**

Run: `npm test && npm run lint`
Expected: all pass; oxlint clean.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/lib/types.ts frontend/src/render/TableBlock.tsx frontend/src/render/Blocks.tsx frontend/src/theme.css frontend/src/render/render.test.tsx
git commit -m "feat(render): colour table cells by the server's threshold band"
```
