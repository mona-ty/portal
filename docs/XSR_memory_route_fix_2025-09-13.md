# XSR — Memory-First Route Recovery Changes (2025-09-13)

## Summary
- Goal: Restore full submarine routes from memory without relying on UI.
- Issue: Only the last 1–2 points (e.g., 15,10) were read; UI screens sometimes expose no route text or render route as split tokens.
- Approach: Enable memory‑only capture and add an in‑struct scanner that reconstructs the planned route (3–5 points) safely from `HousingWorkshopSubmersibleSubData`.
- Result: JSON now contains full numeric routes; RouteShort shows readable letters even without alias data.

## Behavior Changes
- Memory Only mode (default ON): `/xsr dump` and auto capture read from memory first; UI enrichment is disabled unless explicitly opted in.
- Planned route recovery: If `CurrentExplorationPoints` is short, scan the struct for a 0‑terminated sequence of 1..255 bytes (length 3–5) that contains the current tail (e.g., 15,10). Adopt the best‑scored candidate.
- RouteShort readability: When alias/map lookup is unavailable, numbers 1..26 map to A..Z; others remain `P<n>`.
- UI enrichment and post‑repair run only when `MemoryOnlyMode=false`.
 - Diagnostic: `/xsr memscan [slot]` prints best candidate summary to chat and detailed candidates to debug log.

## Implementation Details
- Added recovery routine:
  - `TryRecoverFullRouteFromMemory(HousingWorkshopSubmersibleSubData* s, List<byte> tail, int currentFieldOffset, out List<int> recovered, out int bestOff, out int bestStride, out bool reversed)`
  - Scans within `sizeof(HousingWorkshopSubmersibleSubData)` only, with stride 1/2/4 and optional window around `CurrentExplorationPoints` offset.
  - Collects 0‑terminated sequences (values 1..255) up to 5 tokens.
  - Tail presence (forward/reverse, contiguous subseq or suffix) is mandatory; candidates without the known tail are discarded.
  - Scores by tail match strength, sequence length, and proximity to known fields; adopts the top candidate.
  - Logs: `route recovered: off=0x.., stride=.., reversed=.., memTail=[..], full=[..]`.
- Integrated into memory capture:
  - `TryCaptureFromMemory`: Build `routeFromMem` from `CurrentExplorationPoints`; then attempt recovery. Merge using existing cache logic (`IsSuffix`/`ContainsContiguousSubsequence`) and save cache on full routes.
  - UI enrichment paths guarded by `MemoryOnlyMode`.
- RouteShort fallback:
  - `BuildRouteShortFromNumbers`: Resolution order = SectorResolver (alias by map) → `Config/RouteNames` → A..Z fallback for 1..26 → `P<n>`.
- Command flow:
  - `OnCmdDump` and auto capture honor `MemoryOnlyMode` and avoid UI unless disabled.

## Config Additions (Configuration.cs)
- `MemoryOnlyMode` (bool, default true): Disable UI enrichment and rely on memory only.
- `MemoryRouteScanEnabled` (bool, default true): Enable struct scanning for planned route recovery.
- `MemoryRouteMinCount` (int, default 3): Minimum tokens for a recovered candidate.
- `MemoryRouteMaxCount` (int, default 5): Maximum tokens to read for a candidate.
 - `MemoryRouteScanWindowBytes` (int, default 0x120): Limit scan to ±window bytes around `CurrentExplorationPoints` (0 to scan full struct).

## Files Changed
- `apps/XIVSubmarinesReturn/src/Plugin.cs`
  - Added recover function and integrated it into memory capture.
  - Guarded UI enrichment and post‑repair with `MemoryOnlyMode`.
  - Enhanced logs and normalized adoption logic to `routeFromMem`.
  - Added A..Z fallback in `BuildRouteShortFromNumbers`.
- `apps/XIVSubmarinesReturn/src/Configuration.cs`
  - Added memory‑related options.
- `apps/XIVSubmarinesReturn/XIVSubmarinesReturn.csproj`
  - Normalized reference `HintPath` separators for cross‑env builds.
- Local build helper (not committed): `apps/XIVSubmarinesReturn/Local.props` to set `DalamudLibPath`.

## Logs & Diagnostics
- Recovery log example:
  - `S1 route recovered: off=0x1C, reversed=False, memTail=[15,10], full=[13,18,15,10,26]`
- Adoption trace example:
  - `S1 route mem=[13,18,15,10,26], cache=[...], adopted=mem, reason=mem, final=[13,18,15,10,26]`
- JSON example:
  - `RouteKey`: `Point-13 - Point-18 - Point-15 - Point-10 - Point-26`
  - `RouteShort`: `M>R>O>J>Z` (via A..Z fallback if alias not available)

## Build & Deployment
- Build: `.NET 9`, Release x64
  - Output: `apps/XIVSubmarinesReturn/bin/x64/Release/net9.0-windows/XIVSubmarinesReturn.dll`
- Dalamud dev testing:
  - Place DLL, `manifest.json`, `icon.png` in Dev Plugins path or register the folder in Dalamud → reload → `/xsr dump` in workshop.

## Verification Checklist
- Memory‑only `/xsr dump` produces `RouteKey` with ≥3 points.
- `xsr_debug.log` contains `route recovered` and recovered list contains current tail (e.g., 15,10).
- `RouteShort` displays letters (A..Z fallback for 1..26) or mapped aliases if available.
- No UI enrichment occurs unless `MemoryOnlyMode=false`.

## Open Risks / Next Steps
- Precision: If multiple in‑struct sequences contain the same tail, a better scorer could incorporate known sector IDs (via Lumina) to penalize unknown values.
- Tooling: Add `/xsr memdump [slot]` for a bounded hex dump around relevant offsets to aid field mapping.
- UX: Expose `MemoryOnlyMode` / scan toggles in UI and add `/xsr setroute|getroute` as a manual fallback for seeding cache.
