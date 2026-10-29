import { test } from "node:test";
import assert from "node:assert/strict";
import { quote, validateStep } from "../src/rebranding.ts";
test("wizard charges only added features and ignores blank lines", () => {
  assert.equal(quote("A\n\nB\n"), 29);
  assert.equal(quote(""), 25);
});
test("wizard validates only the current step and accepts a simple request", () => {
  assert.equal(
    validateStep(0, {
      discord: "hi",
      brandName: "Nova",
      changes: "A purple menu with a new logo",
    }),
    null,
  );
  assert.equal(
    validateStep(0, { discord: "", brandName: "Nova", changes: "Short" }).field,
    "discord",
  );
  assert.equal(
    validateStep(1, {
      newFeatures: "",
      removeFeatures: "Delete radar",
      aesthetics: "",
    }),
    null,
  );
  assert.equal(
    validateStep(1, { newFeatures: Array(21).fill("A").join("\n") }).field,
    "newFeatures",
  );
});
