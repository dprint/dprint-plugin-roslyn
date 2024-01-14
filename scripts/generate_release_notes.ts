import { generateChangeLog } from "https://raw.githubusercontent.com/dprint/automation/0.9.0/changelog.ts";

const version = Deno.args[0];
const checksum = Deno.args[1];
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
