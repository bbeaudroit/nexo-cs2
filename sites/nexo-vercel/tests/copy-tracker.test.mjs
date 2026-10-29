import { test } from "node:test";
import assert from "node:assert/strict";
import { createCopyTracker } from "../src/copy-tracker.ts";
test("first concurrent copies wait for the browser identity response before the next request", async () => {
  let cookie = "",
    release;
  const firstResponse = new Promise((r) => (release = r)),
    seen = [];
  const track = createCopyTracker();
  const a = track(async () => {
    seen.push(cookie || "new");
    await firstResponse;
    cookie = "browser-1";
  });
  const b = track(async () => {
    seen.push(cookie || "new");
  });
  await Promise.resolve();
  assert.deepEqual(seen, ["new"]);
  release();
  await Promise.all([a, b]);
  assert.deepEqual(seen, ["new", "browser-1"]);
});
test("a failed tracking request does not block future copies", async () => {
  const track = createCopyTracker();
  let sent = false;
  await assert.rejects(
    track(async () => {
      throw Error("Offline");
    }),
  );
  await track(async () => {
    sent = true;
  });
  assert.equal(sent, true);
});
