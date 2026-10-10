#!/usr/bin/env -S deno run -A
import $ from "jsr:@david/dax@^0.47.0";

const rootDir = $.path(import.meta.dirname!).join("..");
const outDir = rootDir.join("npm-dist");
const version = extractVersionFromCsproj(
  await rootDir.join("DprintPluginRoslyn/DprintPluginRoslyn.csproj").readText(),
);

outDir.mkdirSync({ recursive: true });
// dprint resolves `npm:@dprint/roslyn@<version>` to the plugin.wasm at the root of the package
await rootDir.join("plugin.wasm").copyFile(outDir.join("plugin.wasm"));
await rootDir.join("README.md").copyFile(outDir.join("README.md"));
await rootDir.join("LICENSE").copyFile(outDir.join("LICENSE"));
await outDir.join("package.json").writeText(
  JSON.stringify(
    {
      name: "@dprint/roslyn",
      version,
      description: "Use Roslyn (the .NET compiler) as a dprint plugin to format C# and VB code.",
      license: "MIT",
      repository: {
        type: "git",
        url: "git+https://github.com/dprint/dprint-plugin-roslyn.git",
      },
      homepage: "https://github.com/dprint/dprint-plugin-roslyn",
      files: ["plugin.wasm", "README.md", "LICENSE"],
    },
    undefined,
    2,
  ) + "\n",
);

console.log("Created npm package:", outDir.toString());

function extractVersionFromCsproj(text: string): string {
  const match = text.match(/<Version>(\d+\.\d+\.\d+)<\/Version>/);
  if (!match) throw new Error("Could not find <Version>X.Y.Z</Version> in .csproj");
  return match[1];
}
