# dprint-plugin-roslyn

[![CI](https://github.com/dprint/dprint-plugin-roslyn/workflows/CI/badge.svg)](https://github.com/dprint/dprint-plugin-roslyn/actions?query=workflow%3ACI)

Wrapper around [Roslyn](https://github.com/dotnet/roslyn) in order to use it as a dprint plugin for C# and Visual Basic code formatting.

## Install

1. Install [dprint](https://dprint.dev/install/)
2. Follow instructions at https://github.com/dprint/dprint-plugin-roslyn/releases/

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
