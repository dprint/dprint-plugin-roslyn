#!/usr/bin/env -S deno run -A
import $ from "jsr:@david/dax@^0.47.0";
import { getChecksum, processPlugin } from "jsr:@dprint/automation@0.11.3";

const pluginName = "dprint-plugin-roslyn";
const mainPackageName = "@dprint/roslyn";
const outDir = "npm-dist";
// where the platform zips are extracted before being repacked as npm sub-packages.
const extractDir = "npm-binaries";

const platforms: processPlugin.Platform[] = [
  "darwin-x86_64",
  "darwin-aarch64",
  "linux-x86_64",
  "linux-x86_64-musl",
  "linux-aarch64",
  "linux-aarch64-musl",
  "windows-x86_64",
];

const rootDir = $.path(import.meta.dirname!).join("..");
const version = extractVersionFromCsproj(
  await rootDir.join("DprintPluginRoslyn/DprintPluginRoslyn.csproj").readText(),
);

const extractRoot = rootDir.join(extractDir);
extractRoot.mkdirSync({ recursive: true });

const platformInputs = await Promise.all(platforms.map(async (platform) => {
  const zipPath = rootDir.join(
    processPlugin.getStandardZipFileName(pluginName, platform),
  );
  const platformDir = extractRoot.join(platform);
  platformDir.mkdirSync({ recursive: true });
  await $`unzip -o ${zipPath.toString()} -d ${platformDir.toString()}`.quiet();
  const binaryName = platform.startsWith("windows-") ? `${pluginName}.exe` : pluginName;
  return {
    platform,
    binaryPath: platformDir.join(binaryName).toString(),
    // roslyn is a self-contained .NET app — the executable references ~200
    // sibling DLLs and runtime files, so we ship the whole dir.
    packageContents: platformDir.toString(),
  };
}));

const result = await processPlugin.createDprintOrgNpmPackages({
  pluginName,
  mainPackageName,
  version,
  outDir: rootDir.join(outDir).toString(),
  platforms: platformInputs,
  packageJsonExtra: {
    description: "Use Roslyn (the .NET compiler) as a dprint plugin to format C# and VB code.",
    license: "MIT",
    repository: {
      type: "git",
      url: "git+https://github.com/dprint/dprint-plugin-roslyn.git",
    },
    homepage: "https://github.com/dprint/dprint-plugin-roslyn",
  },
});

// hash the main tarball so the release-notes step can embed it in the
// `npm:@dprint/roslyn@<version>/plugin.json@<hash>` reference users paste
// into dprint.json. This is the hash dprint verifies before extracting.
const mainPackageChecksum = await getChecksum(await Deno.readFile(result.mainPackageTarball));

// emit a manifest so publish_npm_packages.ts knows the order and which
// tarballs to publish without having to re-derive it from the directory.
await Deno.writeTextFile(
  rootDir.join(outDir, "publish-manifest.json").toString(),
  JSON.stringify(
    {
      mainPackageName,
      version,
      subPackageTarballs: result.subPackageTarballs,
      mainPackageTarball: result.mainPackageTarball,
      mainPackageChecksum,
    },
    undefined,
    2,
  ) + "\n",
);

console.log("Main package tarball:", result.mainPackageTarball);
console.log("Sub-package tarballs:");
for (const tgz of result.subPackageTarballs) {
  console.log("  " + tgz);
}

function extractVersionFromCsproj(text: string): string {
  const match = text.match(/<Version>(\d+\.\d+\.\d+)<\/Version>/);
  if (!match) throw new Error("Could not find <Version>X.Y.Z</Version> in .csproj");
  return match[1];
}
