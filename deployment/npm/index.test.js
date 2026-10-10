// @ts-check
const assert = require("assert");
const fs = require("fs");
const getPath = require("./index").getPath;

// The module can't be run with @dprint/formatter because it needs the WASI
// imports that the dprint CLI provides, so only check it's a dprint plugin.
const wasmModule = new WebAssembly.Module(fs.readFileSync(getPath()));
const exportNames = WebAssembly.Module.exports(wasmModule).map(e => e.name);
const importModules = new Set(WebAssembly.Module.imports(wasmModule).map(i => i.module));

assert.ok(exportNames.includes("dprint_plugin_version_4"));
assert.ok(exportNames.includes("format"));
assert.deepStrictEqual([...importModules].sort(), ["dprint", "wasi_snapshot_preview1"]);
