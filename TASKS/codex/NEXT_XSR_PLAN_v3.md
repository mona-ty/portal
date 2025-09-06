# \# NEXT\_XSR\_PLAN\_v3 — Build solid, Notion key modes, schema validator, polish

# 

# \## P0: Build sanity (critical)

# Edits: Plugin.cs

# \- Remove old Plugin.cs variant (no shared HttpClient / no GCAL guard).

# \- Fix duplicate prints \& missing braces in OnCmdConfig, OnCmdConfigOpen, CmdDumpFromMemory, CmdLearnNames.

# DoD: Release/x64 build succeeds without errors.

# 

# \## P1: Notion upsert key modes + schema validation

# Edits: Configuration.cs, Services/NotionClient.cs, Plugin.UI.cs

# \- Add enum NotionKeyMode { PerSlot (default), PerSlotRoute, PerVoyage }.

# \- BuildStableId(): switch by mode:

# &nbsp; - PerSlot => $"{Character}|{World}|{Slot}" (fallback: $"{Name}|{Slot}")

# &nbsp; - PerSlotRoute => $"{Character}|{World}|{Slot}|{RouteKey}"

# &nbsp; - PerVoyage => current (Name|EtaUnix|Slot)

# \- Add EnsureDatabasePropsAsync(): GET /v1/databases/{id}, verify Name/Slot/ETA/Route/Rank/ExtId/Remaining/World/Character/FC.

# \- UI: add "Validate properties" button and show result in \_uiStatus.

# DoD: PerSlot updates same page for same slot; validator reports missing/mismatch.

# 

# \## P2: Formatter cleanup

# Edits: Services/EtaFormatter.cs

# \- Remove duplicate assignment to RemainingText.

# \- Add comment for local time vs UTC calculation.

# DoD: No duplicate write; sample JSON shows correct RemainingText.

# 

# \## P3: Parser unification \& trace

# Edits: Extractors.cs, Plugin.cs (selftest)

# \- Consolidate TryParseDuration into TryParseDurationEx (or make it a thin wrapper).

# \- Ensure extract\_log.txt always starts with addon name and counts.

# DoD: Self-test passes for JP/EN/HH:MM(:SS); trace file contains header line.

# 

# \## P4: Packaging (zip)

# \- Ensure packager output not locked; use temp path or skip zip via preset.

# DoD: One-shot build+zip or documented zip-skip path works consistently.

# 

