set shell := ['powershell', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command']

# 使い方: just new <PluginName>
new name:
    ./tools/new-dalamud-plugin.ps1 -Name {{name}} -Dir .

