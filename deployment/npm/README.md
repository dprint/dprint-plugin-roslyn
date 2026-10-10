# @dprint/roslyn

npm distribution of [dprint-plugin-roslyn](https://github.com/dprint/dprint-plugin-roslyn), which formats C# and Visual Basic code with [Roslyn](https://github.com/dotnet/roslyn).

## Use with the dprint CLI

Requires [dprint](https://dprint.dev/install/) 0.61.0 or later.

Run `dprint add roslyn` or add the package to the `"plugins"` array of a dprint configuration file:

```jsonc
{
  // ...etc...
  "plugins": [
    "npm:@dprint/roslyn@<version>"
  ]
}
```

## Use with @dprint/formatter

Requires [@dprint/formatter](https://github.com/dprint/js-formatter) 0.6.0 or later.

```ts
import { createFromBuffer } from "@dprint/formatter";
import { getPath } from "@dprint/roslyn";
import * as fs from "fs";

const buffer = fs.readFileSync(getPath());
const formatter = createFromBuffer(buffer);

console.log(formatter.formatText({
  filePath: "file.cs",
  fileText: "namespace Test    {   }",
}));
```
