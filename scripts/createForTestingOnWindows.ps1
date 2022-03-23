# Script for quickly creating the plugin for testing purposes on Windows
# To run:
# 1. Run `./scripts/createForTestingOnWindows.ps1`
# 2. Update dprint.json to point at ./plugin.exe-plugin then update checksum
#    as shown when initially run.

$ErrorActionPreference = "Stop"

dotnet build DprintPluginRoslyn -c Release --runtime win-x64
Compress-Archive -Force -Path DprintPluginRoslyn/bin/Release/net6.0/win-x64/* -DestinationPath dprint-plugin-roslyn-x86_64-pc-windows-msvc.zip
deno run --allow-read=. --allow-write=. ./scripts/create_plugin_file.ts --test
