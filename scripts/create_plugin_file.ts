import { processPlugin } from "jsr:@dprint/automation@0.10.3";
import * as path from "jsr:@std/path@1";

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
    "linux-x86_64-musl",
    "linux-aarch64",
    "linux-aarch64-musl",
    "windows-x86_64",
  ],
  isTest: Deno.args.some(a => a == "--test"),
});
