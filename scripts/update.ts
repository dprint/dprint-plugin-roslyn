/**
 * This script checks for any Roslyn updates and then automatically
 * publishes a new version of the plugin if so.
 *
 * Requires the WASI_SDK_PATH environment variable and dprint in order to
 * build and test the Wasm module.
 */
import $ from "jsr:@david/dax@^0.47.0";
import { aiFixRoslynUpdate } from "./ai_fix.ts";
import { bumpVersion, csprojPath, publishVersion, rootDir } from "./utils.ts";

const wasmPath = rootDir.join("DprintPluginRoslyn/bin/Release/net10.0/wasi-wasm/publish/dprint-plugin-roslyn.wasm");

Deno.chdir(rootDir.toString());

const previousRoslynVersion = getRoslynVersion();

$.logStep("Installing dotnet-outdated-tool...");
// fails when already installed
await $`dotnet tool install --global dotnet-outdated-tool`.noThrow();

$.logStep("Upgrading packages...");
await $`dotnet outdated --upgrade`;

if (!await hasFileChanged(csprojPath.toString())) {
  $.log("No changes.");
  Deno.exit(0);
}

const roslynVersion = getRoslynVersion();
const isPatchBump = isPatchBumpOrSame(previousRoslynVersion, roslynVersion);
$.log(`Found changes (Roslyn ${previousRoslynVersion} -> ${roslynVersion}).`);

// Verify the update. New and removed formatting options need no changes
// because they're found with reflection and the config schema is generated,
// so only a failure gets an AI fix attempt.
$.logStep("Running checks (test + wasm build + wasm test)...");
const checks = await runChecks();

if (!checks.passed) {
  $.logStep("Update failed the checks — running AI fix...");
  await aiFixRoslynUpdate({
    fromVersion: previousRoslynVersion,
    toVersion: roslynVersion,
    // hand the failing output to the AI so it can go straight to fixing
    // instead of re-running the checks just to rediscover the errors.
    checkOutput: checks.output,
    checkCommands: checkCommands().map((check) => check.description),
  });

  // the AI must leave the project in a passing state, otherwise fail the
  // workflow (nothing gets published and the maintainer is notified).
  $.logStep("Re-running checks after AI changes...");
  await assertChecks();
}

if (Deno.args.includes("--skip-publish")) {
  Deno.exit(0);
}

$.logStep("Committing Roslyn update commit...");
await $`git add .`;
const message = previousRoslynVersion === roslynVersion
  ? "fix: update dependencies"
  : `${isPatchBump ? "fix" : "feat"}: update to Roslyn ${roslynVersion}`;
await $`git commit -m ${message}`;

$.logStep("Bumping version...");
const newVersion = bumpVersion(isPatchBump ? "patch" : "minor");
await publishVersion(newVersion);

// the checks that must pass before publishing. The Wasm build and tests are
// included because that is what actually ships and an update may only break
// when compiled ahead of time. `inheritPiped` + `captureCombined` streams the
// output to the CI log live while also capturing it so a failure can be handed
// to the AI.
async function runChecks(): Promise<{ passed: boolean; output: string }> {
  const results = [];
  for (const check of checkCommands()) {
    const result = await check.run()
      .stdout("inheritPiped")
      .stderr("inheritPiped")
      .captureCombined()
      .noThrow();
    results.push(result);
    // the later checks depend on the earlier ones
    if (result.code !== 0) {
      break;
    }
  }
  const failures = results.filter((r) => r.code !== 0);
  return {
    passed: failures.length === 0,
    output: failures.map((r) => r.combined).join("\n\n"),
  };
}

// same checks as `runChecks`, but throws on the first failure so the workflow
// aborts before anything is committed, tagged, or published.
async function assertChecks(): Promise<void> {
  for (const check of checkCommands()) {
    await check.run();
  }
}

// a function (not a top-level const) so it is hoisted -- `runChecks` is called
// from top-level code above before a const declared here would be initialized.
function checkCommands() {
  return [
    {
      // also updates the config schema for any options Roslyn added or removed
      description: "UPDATE=1 dotnet test",
      run: () => $`dotnet test`.env("UPDATE", "1"),
    },
    {
      description: "dotnet publish DprintPluginRoslyn -c Release -r wasi-wasm",
      run: () => $`dotnet publish DprintPluginRoslyn -c Release -r wasi-wasm`,
    },
    {
      description: `DPRINT_ROSLYN_WASM_PATH=${wasmPath} dotnet test --filter TestCategory=Wasm`,
      run: () => $`dotnet test --filter TestCategory=Wasm`.env("DPRINT_ROSLYN_WASM_PATH", wasmPath.toString()),
    },
  ];
}

function getRoslynVersion() {
  const match = csprojPath.readTextSync().match(
    /Include="Microsoft\.CodeAnalysis\.CSharp\.Workspaces" Version="([^"]+)"/,
  );
  const version = match?.[1];
  if (version == null) {
    throw new Error("Could not find the Roslyn version in the project file.");
  }
  return version;
}

function isPatchBumpOrSame(from: string, to: string) {
  const [fromMajor, fromMinor] = from.split(".");
  const [toMajor, toMinor] = to.split(".");
  return fromMajor === toMajor && fromMinor === toMinor;
}

async function hasFileChanged(file: string) {
  const result = await $`git diff --exit-code ${file}`.noThrow().quiet();
  return result.code !== 0;
}
