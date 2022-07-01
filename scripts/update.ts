import * as path from "https://deno.land/std@0.130.0/path/mod.ts";
import * as semver from "https://deno.land/x/semver@v1.4.0/mod.ts";

const rootDirPath = path.dirname(path.dirname(path.fromFileUrl(import.meta.url)));

try {
  await runCommand("dotnet tool install --global dotnet-outdated-tool".split(" "));
} catch {
  // ignore, installed
}

await runCommand("dotnet outdated --upgrade".split(" "));

if (!hasFileChanged("./DprintPluginRoslyn/DprintPluginRoslyn.csproj")) {
  console.log("No changes.");
  Deno.exit(0);
}

const newVersion = await bumpMinorVersion();

// run the tests
await runCommand("dotnet test".split(" "));

// release
await runCommand("git add .".split(" "));
await runCommand(`git commit -m ${newVersion}`.split(" "));
await runCommand(`git push upstream main`.split(" "));
await runCommand(`git tag ${newVersion}`.split(" "));
await runCommand(`git push upstream ${newVersion}`.split(" "));

async function bumpMinorVersion() {
  const projectFile = path.join(rootDirPath, "./DprintPluginRoslyn/DprintPluginRoslyn.csproj");
  const text = await Deno.readTextFile(projectFile);
  const versionRegex = /\<Version\>([0-9]+\.[0-9]+\.[0-9]+)\</;
  const currentVersion = text.match(versionRegex)?.[1];
  if (currentVersion == null) {
    throw new Error("Could not find version.");
  }
  const newVersion = semver.parse(currentVersion)!.inc("minor").toString();
  const newText = text.replace(versionRegex, `<Version>${newVersion}<`);
  await Deno.writeTextFile(projectFile, newText);
  return newVersion;
}

async function hasFileChanged(file: string) {
  try {
    await runCommand(["git", "diff", "--exit-code", file]);
    return false;
  } catch {
    return true;
  }
}

async function runCommand(cmd: string[]) {
  const p = Deno.run({
    cmd,
    cwd: rootDirPath,
  });
  const status = await p.status();
  p.close();
  if (status.code !== 0) {
    throw new Error("Failed.");
  }
}
