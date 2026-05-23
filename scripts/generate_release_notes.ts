import { generateChangeLog } from "jsr:@dprint/automation@0.11.3";

const version = Deno.args[0];
const checksum = Deno.args[1];

// optional npm install block; only emitted if create_npm_packages.ts has
// run and produced a manifest with the main package's tarball checksum.
let npmBlock = "";
try {
  const manifest = JSON.parse(await Deno.readTextFile("npm-dist/publish-manifest.json")) as {
    mainPackageName: string;
    mainPackageChecksum: string;
  };
  npmBlock = `
   Alternatively, run \`dprint config add npm:${manifest.mainPackageName}\`, which will update the config file as follows:
   \`\`\`jsonc
   {
     // etc...
     "plugins": [
       "npm:${manifest.mainPackageName}@${version}/plugin.json@${manifest.mainPackageChecksum}"
     ]
   }
   \`\`\`
`;
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

1. Specify the plugin url and checksum in the \`"plugins"\` array or run \`dprint config add roslyn\`.
   \`\`\`jsonc
   {
     // etc...
     "plugins": [
       "https://plugins.dprint.dev/roslyn-${version}.json@${checksum}"
     ]
   }
   \`\`\`
${npmBlock}2. Add a "roslyn" configuration property if desired.
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
