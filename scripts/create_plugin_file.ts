import * as path from "https://deno.land/std@0.146.0/path/mod.ts";
import { processPlugin } from "https://raw.githubusercontent.com/dprint/automation/0.4.0/mod.ts";

const currentDirPath = path.dirname(path.fromFileUrl(import.meta.url));
const projectFile = path.join(currentDirPath, "../DprintPluginRoslyn/DprintPluginRoslyn.csproj");

const packageText = await Deno.readTextFile(projectFile);
const version = packageText.match(/\<Version\>(\d+\.\d+\.\d+)<\/Version\>/)?.[1];

if (version == null || !/^\d+\.\d+\.\d+$/.test(version)) {
  throw new Error("Error extracting version.");
}

await processPlugin.createDprintOrgProcessPlugin({
  pluginName: "dprint-plugin-roslyn",
  version,
  platforms: [
    "darwin-x86_64",
    "darwin-aarch64",
    "linux-x86_64",
    "linux-aarch64",
    "windows-x86_64",
  ],
  isTest: Deno.args.some(a => a == "--test"),
});
