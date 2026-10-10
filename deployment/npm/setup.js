// @ts-check
const fs = require("fs");
const path = require("path");
const args = process.argv.slice(2);
const rootDir = path.join(__dirname, "../../");
const wasmPath = path.join(
  rootDir,
  "DprintPluginRoslyn/bin/Release/net10.0/wasi-wasm/publish/dprint-plugin-roslyn.wasm",
);
// dprint resolves `npm:@dprint/roslyn@<version>` to the plugin.wasm at the root of the package
fs.copyFileSync(wasmPath, path.join(__dirname, "plugin.wasm"));
fs.copyFileSync(path.join(rootDir, "LICENSE"), path.join(__dirname, "LICENSE"));

if (args.length > 0) {
  // update the version based on the first argument
  const packageJsonPath = path.join(__dirname, "package.json");
  const packageJsonText = fs.readFileSync(packageJsonPath, "utf8");
  const packageJson = JSON.parse(packageJsonText);
  if (args[0] === "sync-version") {
    const csprojPath = path.join(rootDir, "DprintPluginRoslyn/DprintPluginRoslyn.csproj");
    const csprojText = fs.readFileSync(csprojPath, "utf8");
    const versionMatch = csprojText.match(/<Version>(\d+\.\d+\.\d+)<\/Version>/);
    if (!versionMatch) {
      throw new Error("Could not find version in DprintPluginRoslyn.csproj");
    }
    packageJson.version = versionMatch[1];
  } else {
    packageJson.version = args[0];
  }
  fs.writeFileSync(packageJsonPath, JSON.stringify(packageJson, undefined, 2) + "\n");
}
