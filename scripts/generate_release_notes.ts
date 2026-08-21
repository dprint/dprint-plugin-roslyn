import { generateChangeLog } from "jsr:@dprint/automation@0.11.3";

const version = Deno.args[0];
const changelog = await generateChangeLog({
  versionTo: version,
});
const text = `## Changes

${changelog}

## Install

1. Run \`dprint add roslyn\`, which will add the plugin to your dprint configuration file.
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
