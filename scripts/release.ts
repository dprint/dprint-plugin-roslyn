/**
 * Bumps the version of the plugin then commits, tags, and pushes it, which
 * causes the CI workflow to publish the release.
 */
import { bumpVersion, publishVersion } from "./utils.ts";

const newVersion = bumpVersion(getReleaseKind());
await publishVersion(newVersion);

function getReleaseKind() {
  if (Deno.args.includes("--patch")) {
    return "patch";
  } else if (Deno.args.includes("--minor")) {
    return "minor";
  } else {
    throw new Error("Expected a --patch or --minor argument.");
  }
}
