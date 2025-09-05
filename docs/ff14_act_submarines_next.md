FF14 ACT Submarines — Next Steps & Notes

- Route key support: apps/ff14-act-submarines/SubmarineTracker.cs:1 adds RouteKey and SetRoute(name, routeKey, tryGetOverride). Once a route is detected, overrides by route/name are applied and ETA is recalculated.
- Reflection discovery: apps/ff14-act-submarines/ReflectionHooks.cs:1 now enumerates FFXIV_ACT_Plugin assembly types, the actual plugin instance in ACT, and logs event names and delegate signatures. This helps identify a stable network event hook point.
- Route detector stub: apps/ff14-act-submarines/RouteDetector.cs:1 is a placeholder; wire actual parsing after we confirm the event payload shape.

How to capture info for implementation
- Build Release and load the DLL in ACT (as before). Open the plugin tab.
- On plugin init, the log shows [Info] lines listing events (with full delegate signatures). Focus on those that look “Network” or “Subscription”.
- Optional: Start the network dump from the tab and open the Workshop list in-game, then stop. Saved to %AppData%\ff14_submarines_act\netdump.

Next work (high level)
- Add real subscriptions in ReflectionHooks: subscribe to EventHandler / EventHandler<T>-style network events and forward their args to RouteDetector.TryExtractRouteKey.
- Implement TryExtractRouteKey(eventArgs): extract submarine name + route key from the event payload that fires when opening the Workshop’s submarine list.
- When found, call SubmarineTracker.SetRoute(name, routeKey, settings.TryGetOverrideMinutes) and refresh the UI list.

What would help
- Share the startup [Info] event list (names + signatures) and the short log produced while opening the Workshop list. With that, we can wire the correct event and implement the parser directly.

