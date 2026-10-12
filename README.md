# dprint-plugin-roslyn

[![npm version](https://img.shields.io/npm/v/@dprint/roslyn.svg)](https://www.npmjs.com/package/@dprint/roslyn) [![CI](https://github.com/dprint/dprint-plugin-roslyn/workflows/CI/badge.svg)](https://github.com/dprint/dprint-plugin-roslyn/actions?query=workflow%3ACI)

Wrapper around [Roslyn](https://github.com/dotnet/roslyn) in order to use it as a dprint plugin for C# and Visual Basic code formatting.

## Install

1. Install [dprint](https://dprint.dev/install/) 0.61.0 or later
1. Run `dprint init`
1. Run `dprint add roslyn`

## Configuration

Specify a "roslyn" configuration property in _dprint.json_ if desired:

```jsonc
{
  // etc...
  "roslyn": {
    "csharp.indentBlock": false,
    "visualBasic.indentWidth": 2,
  },
}
```

C# configuration uses the [`CSharpFormattingOptions`](https://docs.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.formatting.csharpformattingoptions?view=roslyn-dotnet) (use `"csharp.<property name goes here>": <value goes here>` in the configuration file).

It does not seem like roslyn supports any VB specific configuration.

### Disabling languages

C# and Visual Basic are both formatted by default. To have the plugin leave the files of a language alone, set its `enabled` option to `false`:

```jsonc
{
  "roslyn": {
    "visualBasic.enabled": false,
  },
}
```

The options are `csharp.enabled` (`.cs` files) and `visualBasic.enabled` (`.vb` files). A disabled language's files are no longer matched by this plugin, so dprint formats them with the next plugin that matches them, if any.

## Development

The plugin is compiled to a Wasm module using [NativeAOT-LLVM](https://github.com/dotnet/runtimelab/tree/feature/NativeAOT-LLVM).

1. Run the tests with `dotnet test`
1. Install version 29 of the [WASI SDK](https://github.com/WebAssembly/wasi-sdk/releases) and set the `WASI_SDK_PATH` environment variable to its directory.
1. Run `dotnet publish DprintPluginRoslyn -c Release -r wasi-wasm`, which creates _DprintPluginRoslyn/bin/Release/net10.0/wasi-wasm/publish/dprint-plugin-roslyn.wasm_
1. Reference the path to the _.wasm_ file in the `"plugins"` array of a dprint configuration file to try it out.
1. Update the generated configuration schema at _deployment/schema.json_ by running `dotnet test` with the `UPDATE=1` environment variable.
1. Run the tests that format with the _.wasm_ file in dprint by setting the `DPRINT_ROSLYN_WASM_PATH` environment variable to its path and running `dotnet test --filter TestCategory=Wasm`. These use the `dprint` on the path or the executable at the `DPRINT_PATH` environment variable.

### Releasing

Run the [release workflow](https://github.com/dprint/dprint-plugin-roslyn/actions/workflows/release.generated.yml), which bumps the version and pushes a tag. The CI workflow then publishes the GitHub release and the npm package for the tag.
