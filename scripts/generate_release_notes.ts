import { generateChangeLog } from "jsr:@dprint/automation@0.11.3";

const version = Deno.args[0];
const checksum = Deno.args[1];

// prefer the npm specifier, which is what `dprint add` outputs. it's only
// available once create_npm_packages.ts has run and produced a manifest with
// the main package's tarball checksum, so fall back to the plugin url.
let pluginSpecifier = `https://plugins.dprint.dev/roslyn-${version}.json@${checksum}`;
try {
  const manifest = JSON.parse(await Deno.readTextFile("npm-dist/publish-manifest.json")) as {
    mainPackageName: string;
    mainPackageChecksum: string;
  };
  pluginSpecifier = `npm:${manifest.mainPackageName}@${version}/plugin.json@${manifest.mainPackageChecksum}`;
} catch (err) {
  if (!(err instanceof Deno.errors.NotFound)) throw err;
}

const changelog = await generateChangeLog({
  versionTo: version,
});
const text = `## Changes

${changelog}

## Install

In a dprint configuration file:

1. Specify the plugin in the \`"plugins"\` array or run \`dprint add roslyn\`.
   \`\`\`jsonc
   {
     // etc...
     "plugins": [
       "${pluginSpecifier}"
     ]
   }
   \`\`\`
2. Add a "roslyn" configuration property if desired.
   \`\`\`jsonc
   {
     // ...etc...
     "roslyn": {
       "csharp.indentBlock": false,
       "visualBasic.indentWidth": 2
     }
   }
   \`\`\`
`;

console.log(text);
