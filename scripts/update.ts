import * as path from "https://deno.land/std@0.130.0/path/mod.ts";

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
  const status = await p.status()
  p.close();
  if (status.code !== 0) {
    throw new Error("Failed.");
  }
}