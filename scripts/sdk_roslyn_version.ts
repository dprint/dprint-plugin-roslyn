/**
 * Finds the version of Roslyn that ships in the latest stable .NET SDK.
 *
 * The plugin uses the same Roslyn version as the SDK so that it formats the
 * same as `dotnet format`. That version is often a prerelease build that's
 * only published to the dnceng `dotnet-tools` feed, so `dotnet outdated`
 * can't be used to find it (it would pick whatever daily build is newest).
 *
 * Run directly to print the version.
 */
import $ from "jsr:@david/dax@^0.47.0";

const releasesIndexUrl = "https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json";

if (import.meta.main) {
  const { sdkVersion, roslynVersion } = await getLatestSdkRoslynVersion();
  $.log(`SDK ${sdkVersion} uses Roslyn ${roslynVersion}`);
}

/** Gets the latest stable .NET SDK version and the Roslyn version it ships. */
export async function getLatestSdkRoslynVersion(): Promise<{ sdkVersion: string; roslynVersion: string }> {
  const sdk = await getLatestStableSdk();
  const depsJson = await getDotnetFormatDepsJson(sdk.version, sdk.archiveUrl);
  const match = depsJson.match(/"Microsoft\.CodeAnalysis\.CSharp\.Workspaces\/([^"]+)"/);
  if (match == null) {
    throw new Error(`Could not find the Roslyn version in dotnet-format.deps.json of SDK ${sdk.version}.`);
  }
  return { sdkVersion: sdk.version, roslynVersion: match[1] };
}

async function getLatestStableSdk() {
  const index = await $.request(releasesIndexUrl).json();
  // previews and release candidates ("go-live") are skipped
  const channel = index["releases-index"].find((c: any) => c["support-phase"] === "active");
  if (channel == null) {
    throw new Error("Could not find an active .NET channel.");
  }
  const sdkVersion: string = channel["latest-sdk"];
  const releases = await $.request(channel["releases.json"]).json();
  const sdk = releases.releases
    .flatMap((r: any) => r.sdks ?? [r.sdk])
    .find((s: any) => s?.version === sdkVersion);
  const file = sdk?.files.find((f: any) => f.rid === "linux-x64" && f.name.endsWith(".tar.gz"));
  if (file == null) {
    throw new Error(`Could not find the linux-x64 archive of SDK ${sdkVersion}.`);
  }
  return { version: sdkVersion, archiveUrl: file.url as string };
}

async function getDotnetFormatDepsJson(sdkVersion: string, archiveUrl: string) {
  const depsPath = `sdk/${sdkVersion}/DotnetTools/dotnet-format/dotnet-format.deps.json`;

  // avoid the download when the SDK is already installed
  const sdkLine = (await $`dotnet --list-sdks`.noThrow().text())
    .split(/\r?\n/)
    .find((line) => line.startsWith(`${sdkVersion} [`));
  if (sdkLine != null) {
    const sdkDir = $.path(sdkLine.slice(sdkLine.indexOf("[") + 1, -1)).join(sdkVersion);
    const localDepsPath = sdkDir.join("DotnetTools/dotnet-format/dotnet-format.deps.json");
    if (localDepsPath.existsSync()) {
      return localDepsPath.readTextSync();
    }
  }

  const tempDir = $.path(await Deno.makeTempDir());
  try {
    $.logStep(`Downloading SDK ${sdkVersion}...`);
    const archivePath = tempDir.join("sdk.tar.gz");
    await $.request(archiveUrl).showProgress().pipeToPath(archivePath);
    // relative paths because git bash's tar treats "C:" as a remote host
    await $`tar -xzf sdk.tar.gz --wildcards ${"*" + depsPath}`.cwd(tempDir);
    return tempDir.join(depsPath).readTextSync();
  } finally {
    await tempDir.remove({ recursive: true });
  }
}
