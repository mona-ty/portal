# XSR Route Extraction — Investigation & Plan (2025-09-13)

## Summary
- Symptom: Route remains partial (O>J i.e., Point-15 -> Point-10) even after cleanup and updates.
- Versions shipped: 1.0.4 → 1.0.5 → 1.0.6 with incremental fixes. Issue persists in 1.0.6.
- Evidence shows memory capture yields only last 2 points; UI text walker did not find any route text on the tested screen.
- Root cause hypothesis: Route chain is not exposed as a single text line on the visible screen (SelectString) and the current DFS-based text scraper does not reconstruct tokenized route chains (letters/chevrons) or route is not present on that screen.

## Environment & Timeline
- Target: apps/XIVSubmarinesReturn (Dalamud plugin)
- API: Dalamud API 13 / .NET 9
- Repro screen: “潜水艦の一覧UI” (SelectString-like list with counts/fuel).
- User cleanup performed: Dev/installed/config dirs cleared; reinstall from custom repo.
- Releases:
  - 1.0.4: Prefer fuller UI route; normalize and update RouteShort/cache; log adoption reason.
  - 1.0.5: After memory capture, also enrich from workshop panels; sort items post-enrich.
  - 1.0.6: Detect unlabeled route chains like “M > R > O > J > Z” (and “P15 > P10 > …”) as route text; normalize.

## Evidence (User-supplied)

### Latest JSON snapshot (1.0.6)
- RouteKey: "Point-15 - Point-10"
- Extra.RouteShort: "O>J"
- Note: "captured from memory (WorkshopTerritory.Submersible)"
- No character/world/FC enrichment.

### xsr_debug.log (excerpts)
- Repeated lines show: `S# route bytes = 15,10` → adopted=mem, final=[15,10]
- No “Adopted UI route” messages (UI not providing fuller info).

### stage_dump.txt (excerpts)
```
## SelectString (idx=1, visible=True)
  潜水艦を選択してください。
　探索機体数：4/4機
　保有燃料数：青燐水バレル　1538樽
## ContextMenu (idx=1, visible=False)
## ContextIconMenu (idx=1, visible=False)
## Tooltip (idx=1, visible=False)
  ルーシッドドリーム
```
- No lines containing names or route chains are visible. This suggests: either
  - the list items (names/route data) are not exposed as simple text nodes here, or
  - the route is not present on this screen; it might exist only on other panels (e.g., SubmersibleExploration/Map/Result) not visible at capture time.

## Current Code (key paths)
- Memory capture (last N points):
  - `src/Plugin.cs` → `TryCaptureFromMemory` (reads `CurrentExplorationPoints[0..]`)
- UI scraping (lines):
  - `src/Plugin.cs` → `TryCaptureFromAddon` (SelectString path + DFS of nodes)
  - `src/Plugin.cs` → `EnrichFromWorkshopPanels` (scans multiple addon names)
  - `src/Plugin.Stage.cs` → `CmdDumpStage` (debug: candidate addons dump)
- Route adoption & cache:
  - `src/Plugin.cs` → merge logic for UI vs memory; `SaveCache`, `TryGetCachedNumbers`, `IsSuffix`
- Extractors (text → fields):
  - `src/Extractors.cs` → name/time/rank/route extraction
  - 1.0.6 added: unlabeled route chain regex `^(?:[A-Za-z]|P\d+)(?:\s*[>＞]\s*(?:[A-Za-z]|P\d+)){2,}$`

## Diagnosis
1. Memory yields only last 2 points (15,10) — consistent in logs.
2. UI route lines are not seen on the SelectString panel dump. Names were previously detected (older `extract_log.txt`), but latest `stage_dump.txt` lacks them, indicating:
   - List items might be components without direct text nodes; tokens (letters/chevrons) may be separate nodes or even textures.
   - Or the route is not present on this screen; it might exist only on other panels (e.g., SubmersibleExploration/Map/Result) not visible at capture time.
3. Our unlabeled-chain detection requires a single text line. If tokens are separate nodes on the same row (e.g., `M`, `>`, `R`, ...), the current collector flattens them as independent lines and fails to assemble a chain.

## Immediate Workarounds (user ops)
- Open a panel that actually shows the full route as text (e.g., Voyage/Exploration panels with labeled “航路/目的地” or concatenated chain) and run `/xsr dump` once. This seeds cache; subsequent memory captures will adopt suffix matches.
- Alternatively, add a manual override (planned below) to set per-slot route chain once, which is then used to enrich partial memory captures.

## Plan — Implementation (Phased)

### Phase A: Robust UI token assembly on SelectString & panels
Goal: Reconstruct route chains even when tokens are separate text nodes with or without separators.
- Add a new collector that gathers text nodes with their coordinates (x,y) and font info.
- Group nodes by approximate row (y proximity within a threshold) and horizontal order.
- Within each row, assemble tokens matching `[A-Z]`, `P\d+`, and separators `>`, `＞` into a chain (require ≥3 tokens), e.g., `M > R > O > J > Z`.
- Feed assembled chains into Extractors with a dedicated hook (bypassing the single-line regex limitation).
- Apply same grouping on workshop panels (CompanyCraftSubmersibleList, FreeCompanyWorkshopSubmersible, SubmersibleExploration* variants) to catch alternative screens.

Changes:
- `Plugin.cs`: add `CollectTextTokensWithPositions(AtkUnitBase*)` to harvest `(text, x, y)`; add `AssembleRouteChains(rows)` returning `List<string>`.
- `TryCaptureFromAddon` / `EnrichFromWorkshopPanels`: call token assembler; when a chain is found for a known name context (nearest preceding name in same row pane), adopt as route.
- Logging: write `route(UI-assembled)=...` lines to `extract_log.txt`.

### Phase B: Manual route commands (fallback)
- Add commands to set route per slot (persisted):
  - `/xsr setroute <slot> <chain>` (e.g., `/xsr setroute 1 M>R>O>J>Z`)
  - `/xsr getroute [slot]` to view cached
- During capture, if memory returns ≤2 points, use cached full route when suffix matches or when no conflict.

Changes:
- `Plugin.cs`: command handlers + validation; persist in `Config.LastRouteBySlot`.

### Phase C: Memory reader improvement (optional)
- Investigate additional fields in `HousingWorkshopSubmersibleSubData` (or adjacent structs) that may expose full planned route (not just last/current two points).
- If found, prefer memory full route; fallback to UI/commands.

Changes:
- `TryCaptureFromMemory`: attempt reading additional arrays; guard by API/struct availability.

## Acceptance Criteria
- On SelectString-only screen: route chain is reconstructed as `RouteShort` with ≥3 tokens (e.g., M>R>O>J>Z) without needing other panels.
- `extract_log.txt` contains `route(UI-assembled)` lines indicating token assembly was successful.
- `xsr_debug.log` shows `Adopted UI route ...` with reason `ui-assembled`.
- Manual command `/xsr setroute` sets and persists a chain; subsequent captures use it to enrich memory-only cases.

## Open Questions / Data Needed
- Are route letters (M/R/O/…) pure text nodes or image glyphs? Current dump lacks them; token assembler using text may still fail if route is texture-based.
- Which addon/variant actually renders the route chain on the target client? If available, a `stage_dump.txt` captured with the route-visible panel will help.

## Files Changed So Far (1.0.4 → 1.0.6)
- `src/Plugin.cs`
  - Prefer fuller UI route, update `RouteShort`, save cache, log reasons.
  - Enrich snapshot after memory capture using workshop panels.
- `src/Extractors.cs`
  - Support unlabeled route chain lines like `M > R > O > J > Z` / `P15 > P10 > …`.
- `repo.json`
  - Version bumps 1.0.4 → 1.0.6 + `LastUpdate` refresh.
- CI workflows
  - Packaging fixes; artifact search; Windows path listing fix.

## Next Steps (Engineering Tasks)
1. Implement token+position collector and row-wise assembler (A).
2. Bind assembled chains to nearest name context and adopt (A).
3. Enhance workshop panels scanning to ensure indices (0..31) and variants are covered (A).
4. Add manual route commands (B).
5. Explore extended memory fields for full route (C, optional).
6. Expand logs for visibility (assembled tokens per row; adoptions; conflicts).

## Repro Steps (for CI/manual)
- Open SelectString-only list; run `/xsr dump`; expect `RouteShort` ≥3 tokens after Phase A.
- If still partial: run `/xsr dumpstage` and inspect route tokens presence; if tokens absent, proceed to Phase C (memory) or require specific panel.

---
This document is intended for an LLM-assisted code iteration, with clear context, hypotheses, and concrete change plan.

