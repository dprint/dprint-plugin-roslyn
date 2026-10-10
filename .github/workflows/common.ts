// steps shared between the workflows

const wasiSdkVersion = "29";
const wasiSdkDir = `wasi-sdk-${wasiSdkVersion}.0-x86_64-linux`;

/** Path to the Wasm module created by `buildWasmCommand`. */
export const wasmPath = "DprintPluginRoslyn/bin/Release/net10.0/wasi-wasm/publish/dprint-plugin-roslyn.wasm";

export const buildWasmCommand = "dotnet publish DprintPluginRoslyn -c Release -r wasi-wasm";

export const testWasmCommand = "dotnet test --filter TestCategory=Wasm";

export const setupDotnetStep = {
  uses: "actions/setup-dotnet@v5",
  with: { "dotnet-version": "10.0.x" },
};

/** Installs what is necessary to build the Wasm module and to test it in dprint. */
export const setupWasmSteps = [
  {
    name: "Install WASI SDK",
    run: [
      `curl -sSfL https://github.com/WebAssembly/wasi-sdk/releases/download/wasi-sdk-${wasiSdkVersion}/${wasiSdkDir}.tar.gz | tar xz -C "$RUNNER_TEMP"`,
      `echo "WASI_SDK_PATH=$RUNNER_TEMP/${wasiSdkDir}" >> "$GITHUB_ENV"`,
    ],
  },
  {
    // requires a version of dprint that provides the WASI imports to Wasm plugins
    name: "Install dprint",
    run: [
      "curl -fsSL https://dprint.dev/install.sh | sh",
      `echo "DPRINT_PATH=$HOME/.dprint/bin/dprint" >> "$GITHUB_ENV"`,
    ],
  },
];
