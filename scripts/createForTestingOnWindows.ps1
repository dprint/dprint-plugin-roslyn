# Script for quickly creating the plugin for testing purposes on Windows
# To run:
# 1. Comment out osx and linux `getPlatformObject` and change the
#    reference line to `reference": `./${zipFileName}`,` in scripts/createPluginFile.js
# 2. Run `./scripts/createForTestingOnWindows.ps1`
# 3. Update dprint.json to point at ./roslyn.exe-plugin then update checksum
#    as shown when initially run.

dotnet build -c Release --runtime win-x64
Compress-Archive -Force -Path DprintPluginRoslyn/bin/Release/netcoreapp3.1/win-x64/* -DestinationPath dprint-plugin-roslyn-x86_64-pc-windows-msvc.zip
node ./scripts/createPluginFile.js
