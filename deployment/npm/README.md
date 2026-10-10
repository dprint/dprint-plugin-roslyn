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

## Getting the path to the Wasm module

```ts
import { getPath } from "@dprint/roslyn";

console.log(getPath());
```

Note the Wasm module can't be used with [@dprint/formatter](https://github.com/dprint/js-formatter) because it requires the WASI imports provided by the dprint CLI.
