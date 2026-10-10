/**
 * This script crudely checks for any Roslyn updates and then automatically
 * publishes a new version of the plugin if so.
 *
 * Requires the WASI_SDK_PATH environment variable and dprint in order to
 * build and test the Wasm module.
 */
import $ from "jsr:@david/dax@^0.47.0";
import { bumpVersion, csprojPath, publishVersion, rootDir } from "./utils.ts";

const wasmPath = rootDir.join("DprintPluginRoslyn/bin/Release/net10.0/wasi-wasm/publish/dprint-plugin-roslyn.wasm");

Deno.chdir(rootDir.toString());

$.logStep("Installing dotnet-outdated-tool...");
// fails when already installed
await $`dotnet tool install --global dotnet-outdated-tool`.noThrow();

$.logStep("Upgrading packages...");
await $`dotnet outdated --upgrade`;

if (!await hasFileChanged(csprojPath.toString())) {
  $.log("No changes.");
  Deno.exit(0);
}

$.log("Found changes.");

// also updates the config schema for any options Roslyn added or removed
$.logStep("Running tests...");
await $`dotnet test`.env("UPDATE", "1");

// an update may only break when compiled ahead of time to Wasm
$.logStep("Building Wasm...");
await $`dotnet publish DprintPluginRoslyn -c Release -r wasi-wasm`;

$.logStep("Running Wasm tests...");
await $`dotnet test --filter TestCategory=Wasm`.env("DPRINT_ROSLYN_WASM_PATH", wasmPath.toString());

// todo: better message here
await $`git add .`;
await $`git commit -m ${"feat: update roslyn plugin"}`;

$.logStep("Bumping version...");
const newVersion = bumpVersion("minor");
await publishVersion(newVersion);

async function hasFileChanged(file: string) {
  const result = await $`git diff --exit-code ${file}`.noThrow().quiet();
  return result.code !== 0;
}
