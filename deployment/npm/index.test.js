// @ts-check
const assert = require("assert");
const createFromBuffer = require("@dprint/formatter").createFromBuffer;
const getPath = require("./index").getPath;

const buffer = require("fs").readFileSync(getPath());
const formatter = createFromBuffer(buffer);

assert.strictEqual(
  formatter.formatText({
    filePath: "file.cs",
    fileText: "namespace Test    {   }\n",
  }),
  "namespace Test { }\n",
);
assert.strictEqual(
  formatter.formatText({
    filePath: "file.vb",
    fileText: "Namespace Test\nClass A\nEnd Class\nEnd Namespace\n",
  }),
  "Namespace Test\n    Class A\n    End Class\nEnd Namespace\n",
);
