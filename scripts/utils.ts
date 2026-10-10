import $ from "jsr:@david/dax@^0.47.0";

export const rootDir = $.path(import.meta.dirname!).parentOrThrow();
export const csprojPath = rootDir.join("DprintPluginRoslyn/DprintPluginRoslyn.csproj");

const versionRegex = /<Version>(\d+)\.(\d+)\.(\d+)<\/Version>/;

/** Bumps the version in the project file and returns the new version. */
export function bumpVersion(kind: "patch" | "minor") {
  const text = csprojPath.readTextSync();
  const match = text.match(versionRegex);
  if (match == null) {
    throw new Error("Could not find <Version>X.Y.Z</Version> in the project file.");
  }
  const [major, minor, patch] = match.slice(1).map((value) => parseInt(value, 10));
  const newVersion = kind === "minor" ? `${major}.${minor + 1}.0` : `${major}.${minor}.${patch + 1}`;
  csprojPath.writeTextSync(text.replace(versionRegex, `<Version>${newVersion}</Version>`));
  return newVersion;
}

/** Commits the version bump and pushes it along with a tag, which the CI workflow publishes. */
export async function publishVersion(version: string) {
  $.logStep(`Committing and publishing ${version}...`);
  await $`git add .`.cwd(rootDir);
  await $`git commit -m ${version}`.cwd(rootDir);
  await $`git push origin main`.cwd(rootDir);
  await $`git tag ${version}`.cwd(rootDir);
  await $`git push origin ${version}`.cwd(rootDir);
}
