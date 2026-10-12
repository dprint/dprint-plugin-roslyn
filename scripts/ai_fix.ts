/**
 * Fixes this plugin after a Roslyn update broke it using OpenAI Codex, in two
 * stages that each run Codex in a session of its own:
 *
 *   1. a Codex agentic session edits the source and runs the checks until they
 *      pass, then
 *   2. a SEPARATE reviewer session reviews the result with Codex, so it can
 *      investigate (read the Roslyn source, grep the repo). It is instructed
 *      to review only and not modify anything. If it finds blocking issues
 *      they are fed back to the stage-1 Codex for another pass, bounded by
 *      `REVIEW_MAX_ROUNDS`. If it still isn't approved, this throws so the
 *      workflow fails and nothing is published.
 *
 * This is only called when the checks fail. New and removed Roslyn formatting
 * options don't need it because the plugin finds them with reflection and the
 * config schema is generated.
 *
 * Both stages use the default model of the Codex CLI, which is installed at
 * its latest version when it is missing, so they move to a new model when
 * Codex does. Set `CODEX_MODEL` or `REVIEW_MODEL` to pin the model of a stage.
 *
 * Codex must NOT commit or push -- `update.ts` captures the working tree
 * changes into the Roslyn update commit afterwards.
 */
import $ from "jsr:@david/dax@^0.47.0";

export interface AiFixOptions {
  /** Roslyn version before the update. */
  fromVersion: string;
  /** Roslyn version being updated to. */
  toVersion: string;
  /** Combined output of the failing checks. */
  checkOutput: string;
  /** Commands of the checks that must pass. */
  checkCommands: string[];
}

/** Max number of times the reviewer may send changes back to Codex. */
const REVIEW_MAX_ROUNDS = 2;

export async function aiFixRoslynUpdate(options: AiFixOptions): Promise<void> {
  const apiKey = requireApiKey();
  await ensureCodexInstalled();
  await codexLogin(apiKey);

  // stage 1: let Codex fix the update.
  await runCodex(buildFixPrompt(options));

  // stage 2: independent second-model review, with a bounded refix loop.
  for (let round = 1;; round++) {
    const review = await reviewChanges(options);
    logReview(review);
    if (review.approved) {
      return;
    }
    if (round > REVIEW_MAX_ROUNDS) {
      const blocking = review.issues.filter((i) => i.severity === "blocking");
      throw new Error(
        `AI reviewer did not approve after ${REVIEW_MAX_ROUNDS} refix round(s). Blocking issues:\n`
          + blocking.map((i) => `  - ${i.description}`).join("\n"),
      );
    }
    $.logStep(`Reviewer requested changes — Codex refix round ${round}...`);
    await runCodex(buildRefixPrompt(review, options));
  }
}

// stage 1: Codex ---------------------------------------------------------------

async function runCodex(prompt: string): Promise<void> {
  const args = ["exec", "--dangerously-bypass-approvals-and-sandbox", "--skip-git-repo-check"];
  const model = Deno.env.get("CODEX_MODEL");
  if (model) {
    args.push("--model", model);
  }
  args.push(prompt);

  $.logStep("Running Codex...");
  await $`codex ${args}`;
}

function buildFixPrompt(options: AiFixOptions): string {
  const { fromVersion, toVersion, checkOutput } = options;
  return [
    `You are updating "dprint-plugin-roslyn", a dprint plugin written in C# that wraps Roslyn (the .NET compiler) to format C# and Visual Basic code. It is compiled ahead of time to a Wasm module with NativeAOT-LLVM.`,
    ``,
    `The Roslyn packages (Microsoft.CodeAnalysis.*) were upgraded from ${fromVersion} to ${toVersion} and the checks now fail. This is almost always because the plugin calls Roslyn's INTERNAL formatting engine, which changes without notice, or because the upgrade uses something that doesn't work after trimming and ahead of time compilation.`,
    ``,
    `The checks currently fail with the output below. Use it as your starting point instead of re-running the checks just to rediscover the errors:`,
    "```",
    truncateHead(checkOutput.trim()),
    "```",
    ``,
    `Your goal: make the checks pass while formatting the same way as before. Do NOT commit or push; only edit files in the working tree.`,
    ``,
    describeWiring(),
    ``,
    `To see what changed in Roslyn, read its source at https://github.com/dotnet/roslyn (clone it into a temporary directory OUTSIDE this repository if that helps). The internal formatting engine is under \`src/Workspaces/Core/Portable/Formatting\` and \`src/Workspaces/SharedUtilitiesAndExtensions/Workspace/Core/Formatting\`, with the language specific parts under \`src/Workspaces/CSharp\` and \`src/Workspaces/VisualBasic\`. The compiled packages are in \`~/.nuget/packages/microsoft.codeanalysis.*\`.`,
    ``,
    `Rules:`,
    `1. Keep the Roslyn packages at ${toVersion}. Do not downgrade or pin them to make the checks pass.`,
    `2. Do NOT delete, skip, or weaken tests to make them pass. Only change a test's expected output when Roslyn itself intentionally formats that code differently in ${toVersion}, and say so in your final message.`,
    `3. Never edit \`deployment/schema.json\` by hand. It is generated and is rewritten by running \`dotnet test\` with the \`UPDATE=1\` environment variable.`,
    `4. Do NOT edit \`README.md\`.`,
    `5. Preserve the existing code style. Public members go above private ones. New comments start lowercase unless multiple sentences.`,
    `6. When done, ALL of these must pass (the Wasm build is what actually ships) — iterate until they are all clean:`,
    ...describeChecks(options),
    `7. Do not change the plugin's own version in the project file, do not run git commit, and do not push.`,
  ].join("\n");
}

function buildRefixPrompt(review: ReviewResult, options: AiFixOptions): string {
  return [
    `An independent reviewer examined your changes to dprint-plugin-roslyn and found issues that must be fixed. Address every blocking issue below, keeping all the checks green. Do not commit or push.`,
    ``,
    `Reviewer summary: ${review.summary}`,
    ``,
    `Issues:`,
    ...review.issues.map((i) => `  - [${i.severity}] ${i.description}`),
    ``,
    describeWiring(),
    ``,
    `The checks that must pass:`,
    ...describeChecks(options),
  ].join("\n");
}

function describeWiring(): string {
  return [
    `How the plugin is wired:`,
    `- \`DprintPluginRoslyn/Formatters/SyntaxFormatter.cs\` -> calls Roslyn's internal formatting engine (\`ISyntaxFormatting\`), because the public \`Formatter.Format\` API requires a workspace whose services are found with reflection that doesn't work after trimming.`,
    `- \`DprintPluginRoslyn/Formatters/CSharpCodeFormatter.cs\` and \`VisualBasicCodeFormatter.cs\` -> parse the text and format it with \`CSharpSyntaxFormatting.Instance\` and \`VisualBasicSyntaxFormatting.Instance\`.`,
    `- \`DprintPluginRoslyn/DprintPluginRoslyn.csproj\` -> the \`<Publicize>\` item (Krafs.Publicizer) is what makes Roslyn's internal types accessible, and the \`wasi-wasm\` property groups configure the NativeAOT-LLVM build.`,
    `- \`DprintPluginRoslyn/Configuration/\` -> resolves the dprint configuration into a Roslyn \`OptionSet\` (\`PluginOptionSet.cs\`), finding the \`CSharpFormattingOptions\` properties with reflection.`,
    `- \`csharp.enabled\` and \`visualBasic.enabled\` -> the plugin's own options for turning a language off, which are not options of Roslyn. Each formatter reads its own in \`ResolveConfiguration\` (\`ConfigurationHelpers.HandleEnabledConfig\`) BEFORE the options that are found with reflection, \`CodeFormatters\` only reports the file extensions of the enabled formatters to dprint and does not format a file of a disabled one, and \`SchemaTests.cs\` writes them to the schema.`,
    `- \`DprintPluginRoslyn/Formatters/CodeFormatters.cs\` -> handles encodings, byte ranges, and line endings around the formatters.`,
    `- \`DprintPluginRoslyn/Serialization/PluginJson.cs\` -> hand written JSON, because reflection-based serialization isn't available after ahead of time compilation.`,
    `- \`DprintPluginRoslyn/WasmExports.cs\` -> the functions dprint calls on the Wasm module.`,
    `- \`DprintPluginRoslyn.Tests/\` -> NUnit tests. The ones in the \`Wasm\` category run the built Wasm module in a real dprint executable and compare its output with the .NET runtime's.`,
  ].join("\n");
}

function describeChecks(options: AiFixOptions): string[] {
  return options.checkCommands.map((command) => `     ${command}`);
}

// keep the tail of long check output -- the errors and the build summary are
// at the end, which is the most useful part for the AI.
function truncateHead(text: string, max = 20_000): string {
  return text.length > max ? `... (truncated)\n${text.slice(text.length - max)}` : text;
}

// stage 2: independent reviewer ------------------------------------------------

interface ReviewResult {
  approved: boolean;
  summary: string;
  issues: ReviewIssue[];
}

interface ReviewIssue {
  severity: "blocking" | "nit";
  description: string;
}

// The reviewer runs Codex too so it can actually investigate (read the Roslyn
// source, grep the repo). Its verdict is captured as JSON via --output-last-message.
//
// NOTE: we deliberately do NOT use `--sandbox read-only` here. That makes Codex
// wrap commands in bubblewrap, which cannot set up its network namespace on the
// GitHub Actions runner ("bwrap: loopback: Failed RTM_NEWADDR: Operation not
// permitted"), so every command the reviewer runs fails before executing. We
// use the same no-sandbox mode as the fixer and enforce read-only via the
// prompt instead. (CI is itself a throwaway VM, and the final check gate in
// update.ts re-verifies whatever ends up in the working tree.)
async function reviewChanges(options: AiFixOptions): Promise<ReviewResult> {
  // the reviewer starts without the context of the fixer, which is what makes
  // it a second opinion. It uses the default model of Codex unless one is set.
  const model = Deno.env.get("REVIEW_MODEL");

  // record intent-to-add so any files Codex created also show in `git diff`.
  await $`git add -N .`.quiet();

  const outputFile = await Deno.makeTempFile({ prefix: "codex-review-", suffix: ".json" });
  try {
    $.logStep(`Reviewing changes with Codex (${model ?? "default model"})...`);
    const args = ["exec", "--skip-git-repo-check", "--dangerously-bypass-approvals-and-sandbox"];
    if (model) {
      args.push("--model", model);
    }
    args.push("--output-last-message", outputFile, buildReviewPrompt(options));
    await $`codex ${args}`;
    return parseReview(await Deno.readTextFile(outputFile));
  } finally {
    await Deno.remove(outputFile).catch(() => {});
  }
}

function buildReviewPrompt(options: AiFixOptions): string {
  return [
    `You are an independent reviewer for dprint-plugin-roslyn, a dprint plugin written in C# that wraps Roslyn (the .NET compiler) to format C# and Visual Basic code and is compiled ahead of time to a Wasm module with NativeAOT-LLVM. The Roslyn packages were just upgraded from ${options.fromVersion} to ${options.toVersion}, which broke the plugin, and another AI edited it to fix that. Review the UNCOMMITTED working-tree changes.`,
    `IMPORTANT: this is REVIEW ONLY. Read any files and run read-only commands (git diff, cat, grep, find, etc.) to investigate, but you MUST NOT edit, create, or delete any files, and MUST NOT run git commit or git push. Leave the working tree exactly as you found it.`,
    ``,
    `Investigate as needed:`,
    `- Run \`git --no-pager diff\` (and \`git status\`) to see exactly what changed, including any new files. The package version changes in the \`.csproj\` files and a regenerated \`deployment/schema.json\` are expected.`,
    `- Verify against the REAL Roslyn ${options.toVersion} source at https://github.com/dotnet/roslyn (clone it into a temporary directory OUTSIDE this repository if that helps).`,
    `- Read the plugin files to confirm they are consistent with each other and with upstream.`,
    ``,
    describeWiring(),
    ``,
    `Verify specifically:`,
    `- The plugin still formats with Roslyn's formatting engine and default formatting rules, the same way as before the upgrade. A change that makes the checks pass by bypassing or stubbing out formatting is a blocking issue.`,
    `- The Roslyn packages were NOT downgraded or pinned below ${options.toVersion}.`,
    `- No tests were deleted, skipped, or weakened. A test's expected output may only change when Roslyn itself intentionally formats that code differently in ${options.toVersion}; anything else is a blocking issue.`,
    `- \`deployment/schema.json\` only has changes that correspond to formatting options Roslyn added, removed, or changed.`,
    `- \`csharp.enabled\` and \`visualBasic.enabled\` still work and are still in the schema: they are the plugin's own options and are not expected to exist in Roslyn.`,
    `- Nothing was added that can't work after trimming and ahead of time compilation (for example reflection-based serialization or workspace services found with reflection).`,
    `- README.md and the plugin's own version in the project file were NOT modified; flag either as a blocking issue.`,
    `- No obvious correctness bugs, and code style matches the surrounding code.`,
    ``,
    `When done, your FINAL message must be ONLY a JSON object (no markdown code fences, no extra prose) of exactly this shape:`,
    `{"approved": true|false, "summary": "one sentence", "issues": [{"severity": "blocking"|"nit", "description": "..."}]}`,
    `Set approved=false if there is any blocking issue; nits alone do not block.`,
  ].join("\n");
}

function parseReview(message: string): ReviewResult {
  const review = JSON.parse(extractJsonObject(message)) as ReviewResult;
  if (typeof review.approved !== "boolean" || !Array.isArray(review.issues)) {
    throw new Error(`Codex review returned malformed JSON:\n${message}`);
  }
  return review;
}

// Codex is instructed to emit only JSON, but tolerate an accidental code fence
// or surrounding prose by extracting the outermost { ... } object.
function extractJsonObject(text: string): string {
  const start = text.indexOf("{");
  const end = text.lastIndexOf("}");
  if (start === -1 || end === -1 || end < start) {
    throw new Error(`Could not find a JSON object in Codex review output:\n${text}`);
  }
  return text.slice(start, end + 1);
}

function logReview(review: ReviewResult): void {
  $.log(`Review: ${review.approved ? "approved" : "changes requested"} — ${review.summary}`);
  for (const issue of review.issues) {
    $.logLight(`  [${issue.severity}] ${issue.description}`);
  }
}

// setup ------------------------------------------------------------------------

function requireApiKey(): string {
  const apiKey = Deno.env.get("OPENAI_API_KEY");
  if (!apiKey) {
    throw new Error("OPENAI_API_KEY is not set. It is required to run the AI fixer and reviewer.");
  }
  return apiKey;
}

async function ensureCodexInstalled(): Promise<void> {
  const found = await $`codex --version`.noThrow().quiet();
  if (found.code === 0) {
    return;
  }
  $.logStep("Installing OpenAI Codex CLI...");
  await $`npm install -g @openai/codex`;
}

// `codex exec` does not read OPENAI_API_KEY on its own -- it needs credentials
// stored via `codex login` first, otherwise its requests go out with no auth
// header and the API returns 401. The key is piped over stdin so it never
// appears in the process arguments.
async function codexLogin(apiKey: string): Promise<void> {
  $.logStep("Authenticating Codex with the OpenAI API key...");
  await $`codex login --with-api-key`.stdinText(apiKey);
}
