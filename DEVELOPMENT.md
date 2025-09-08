XIV Submarines Return — Development Notes

Overview
- Purpose: capture FC workshop submarine status from UI/memory and write a JSON snapshot for external tools.
- Core: minimal capture + JSON bridge. Optional integrations (Discord, Notion; Google Calendar via compile flag).

Build
- Prerequisites: .NET 9 SDK, Dalamud dev libraries on your machine.
- Create `Local.props` at repo root or next to the csproj with:
  <Project>
    <PropertyGroup>
      <DalamudLibPath>$(APPDATA)\XIVLauncher\addon\Hooks\dev</DalamudLibPath>
      <!-- Optional: auto‑copy after build -->
      <DevPluginsDir>$(APPDATA)\XIVLauncher\devPlugins\XIVSubmarinesReturn</DevPluginsDir>
    </PropertyGroup>
  </Project>
- Build: dotnet build -c Release -p:Platform=x64

Runtime Basics
- Commands: /xsr (dump, ui, open, addon <name>, version, probe, dumpstage), and direct /subdump /subopen /subaddon /subcfg.
- Output JSON: %AppData%\XIVSubmarinesReturn\bridge\submarines.json
- Auto‑capture: when specified addon is visible (configurable, with memory fallback if enabled).

JSON Schema (subset)
- SubmarineSnapshot
  - SchemaVersion: int (2)
  - PluginVersion: string
  - CapturedAt: ISO datetime
  - Source: "dalamud"
  - Items: array of SubmarineRecord
- SubmarineRecord
  - Name: string
  - RouteKey: string (e.g., "Point-13 - Point-18 - Point-15")
  - DurationMinutes: int? (remaining)
  - Rank: int?
  - Slot: int? (1..4)
  - EtaUnix: long? (epoch seconds)
  - Extra: map { "EtaLocal", "EtaLocalFull", "RemainingText", "RouteShort" }

Optional Integrations
- Discord: enable + set webhook URL in config; sends snapshot/alarm messages.
- Notion: enable + token + database ID; upserts using configured property names.
- Google Calendar: behind compile symbol XSR_FEAT_GCAL (off by default).

Code Map
- src/Plugin.cs: plugin entry, commands, capture pipeline, auto‑capture tick.
- src/Extractors.cs: text parsing from UI dumps; duration/route/name heuristics.
- src/Services/
  - EtaFormatter.cs: enrich with ETA/short route.
  - AlarmScheduler.cs: in‑game/Discord alarms.
  - DiscordNotifier.cs: webhook posting (rate‑limit aware).
  - NotionClient.cs, GoogleCalendarClient.cs: external APIs (optional).
- src/UI/: ImGui bindings, overview/debug table, theming/widgets.
- src/Sectors/: sector alias resolution (Lumina Excel + AliasIndex.json), Mogship importer.

Recent Cleanup (this branch)
- Removed duplicate TrySetIdentity call on dump.
- Removed self‑test command and UI triggers (no functional dependency).
- Simplified Discord notifier: fixed formatting around helpers and removed an unused line builder.
- No behavior changes for capture/JSON output.

Testing
- Manual: use /xsr dump on the relevant UI, verify JSON updates and ETA/Route fields.
- Discord/Notion: enable in settings and trigger /xsr dump or auto‑capture.

Notes
- Many strings are localized/JP; mojibake in README can occur depending on editor/encoding; source is UTF‑8.
