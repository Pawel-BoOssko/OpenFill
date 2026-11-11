// Offline Node test of the injected browser action runtime. No browser or network.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const source = fs.readFileSync(path.join(__dirname, "..", "src", "OpenFill.Core",
  "Assets", "actions__v0.3__2026-10-05_1515.js"), "utf8");
let focusCount = 0;
const element = {
  nodeName: "BUTTON",
  scrollIntoView() {},
  getAttribute() { return null; },
  focus() { focusCount++; }
};
const window = {};
const document = {
  querySelector(selector) {
    return selector.includes("of1") ? element : null;
  }
};
vm.runInNewContext(source, { window, document, Promise, setTimeout });
const actMany = window.__openfill.actMany;

(async () => {
  const stopped = await actMany({ steps: [
    { id: "of1", action: "focus" },
    { id: "of1", action: "key" },
    { id: "of1", action: "focus" }
  ] });
  assert.equal(stopped.ok, false);
  assert.equal(stopped.results.length, 2);
  assert.equal(stopped.stopped_at, 2);
  assert.equal(stopped.repeat_action_automatically, false);
  assert.equal(focusCount, 1);
  assert.equal(stopped.results[1].error, "UNKNOWN_ACTION");

  const missing = await actMany({ steps: [
    { id: "missing", action: "focus" },
    { id: "of1", action: "focus" }
  ] });
  assert.equal(missing.results.length, 1);
  assert.equal(missing.results[0].error, "NOT_FOUND");
  assert.equal(focusCount, 1);

  const noneditable = await actMany({ steps: [
    { id: "of1", action: "set", value: "must-not-write" },
    { id: "of1", action: "focus" }
  ] });
  assert.equal(noneditable.ok, false);
  assert.equal(noneditable.results.length, 1);
  assert.equal(noneditable.results[0].error, "NOT_EDITABLE");
  assert.equal(focusCount, 1);

  const completed = await actMany({ steps: [
    { id: "of1", action: "focus" },
    { id: "of1", action: "focus" }
  ] });
  assert.equal(completed.ok, true);
  assert.equal(completed.results.length, 2);
  assert.equal(focusCount, 3);

  const empty = await actMany({ steps: [] });
  assert.equal(empty.ok, true);
  assert.equal(empty.results.length, 0);
  console.log("ARCHFLOW_LEGACY_BATCH_FAILFAST_5_CHECKS_OK");
})().catch(err => { console.error(err); process.exitCode = 1; });
