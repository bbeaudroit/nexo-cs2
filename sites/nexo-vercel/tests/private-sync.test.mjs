import test from "node:test";
import assert from "node:assert/strict";
import express from "express";
import { createRequire } from "node:module";
import { createPrivateSyncRouter } from "../server/private-sync.mjs";
import { createPrivateSyncStore } from "../server/private-sync-store.mjs";
const { encodeConfig } = createRequire(import.meta.url)(
  "../server/config-contract/electron/profile-share.cjs",
);
const capability = "NEXOSYNC1." + Buffer.alloc(32, 1).toString("base64url");
function memory() {
  const map = new Map();
  return {
    map,
    async read(k) {
      return structuredClone(map.get(k) || null);
    },
    async compareAndSwap(k, etag, value) {
      if ((map.get(k)?.etag ?? null) !== etag) return false;
      map.set(k, {
        etag: String(Number(etag || 0) + 1),
        value: structuredClone(value),
      });
      return true;
    },
  };
}
async function serve(store, fn) {
  const app = express();
  app.use("/sync", createPrivateSyncRouter({ store }));
  const server = app.listen(0);
  await new Promise((r) => server.once("listening", r));
  const base = `http://127.0.0.1:${server.address().port}/sync`;
  const request = (
    path = "",
    method = "GET",
    body,
    auth = capability,
    extra = {},
  ) =>
    fetch(base + path, {
      method,
      headers: {
        authorization: "Bearer " + auth,
        "content-type": "application/json",
        ...extra,
      },
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
  try {
    await fn(request);
  } finally {
    await new Promise((r) => server.close(r));
  }
}
const body = (name = "Test") => ({
  code: encodeConfig({ name, author: "Tester", settings: {} }),
  expectedRevision: null,
});
test("capability auth before storage, isolation and private metadata", async () => {
  const store = memory();
  await serve(store, async (r) => {
    assert.equal((await r("", "GET", undefined, "alice")).status, 401);
    assert.equal(store.map.size, 0);
    const saved = await (await r("", "PUT", body())).json();
    assert.equal(saved.item.revision, 0);
    assert.equal((await (await r()).json()).items[0].code, undefined);
    assert.equal(
      (
        await (
          await r(
            "",
            "GET",
            undefined,
            "NEXOSYNC1." + Buffer.alloc(32, 2).toString("base64url"),
          )
        ).json()
      ).items.length,
      0,
    );
    assert.match(
      (await (await r("/" + saved.item.id)).json()).code,
      /^NEXO1\./,
    );
    assert.equal(
      JSON.stringify([...store.map.keys()]).includes(capability),
      false,
    );
  });
});
test("CAS races, tombstones and no stale resurrection", async () =>
  serve(memory(), async (r) => {
    const b = body();
    const first = await (await r("", "PUT", b)).json();
    const id = first.item.id;
    const races = await Promise.all([
      r("", "PUT", { ...b, expectedRevision: 0 }),
      r("", "PUT", { ...b, expectedRevision: 0 }),
    ]);
    assert.deepEqual(races.map((x) => x.status).sort(), [200, 409]);
    assert.equal(
      (await r("/" + id, "DELETE", { expectedRevision: 1 })).status,
      200,
    );
    assert.equal((await r("/" + id)).status, 404);
    assert.equal((await r("", "PUT", b)).status, 409);
    assert.equal(
      (await r("", "PUT", { ...b, expectedRevision: 1 })).status,
      409,
    );
    assert.equal(
      (await r("", "PUT", { ...b, expectedRevision: 2 })).status,
      200,
    );
  }));
test("validation, cross-site, size and fail closed", async () => {
  await serve(memory(), async (r) => {
    for (const b of [
      { code: "bad", expectedRevision: null },
      { ...body(), expectedRevision: -1 },
      { ...body(), expectedRevision: "0" },
    ])
      assert.equal((await r("", "PUT", b)).status, 400);
    assert.equal(
      (
        await r("", "PUT", body(), capability, {
          origin: "https://evil.example",
        })
      ).status,
      403,
    );
    assert.equal(
      (await r("", "PUT", { code: "x".repeat(103000), expectedRevision: null }))
        .status,
      413,
    );
  });
  await serve(null, async (r) => {
    assert.equal((await r("", "GET", undefined, "bad")).status, 401);
    assert.equal((await r()).status, 503);
  });
});
test("50 identity bound includes durable tombstones", async () =>
  serve(memory(), async (r) => {
    for (let i = 0; i < 50; i++)
      assert.equal((await r("", "PUT", body("CFG" + i))).status, 201);
    const first = (await (await r()).json()).items[0];
    assert.equal(
      (await r("/" + first.id, "DELETE", { expectedRevision: 0 })).status,
      200,
    );
    assert.equal((await r("", "PUT", body("Overflow"))).status, 507);
    assert.equal((await r("", "PUT", body(first.name))).status, 409);
  }));
test("Blob adapter uses private access and conditional overwrite", async () => {
  const calls = [];
  const sdk = {
    async get() {
      return null;
    },
    async put(...args) {
      calls.push(args);
    },
  };
  const store = createPrivateSyncStore({ storeId: "store_test", sdk });
  assert.equal(await store.read("a".repeat(64)), null);
  assert.equal(
    await store.compareAndSwap("a".repeat(64), null, { entries: {} }),
    true,
  );
  assert.equal(calls[0][2].access, "private");
  assert.equal(calls[0][2].allowOverwrite, false);
  await store.compareAndSwap("a".repeat(64), "etag", { entries: {} });
  assert.equal(calls[1][2].ifMatch, "etag");
  assert.equal(createPrivateSyncStore({ token: "", storeId: "" }), null);
});
test("unavailable store hides errors; rate limits and invalid JSON are bounded", async () => {
  await serve(
    {
      async read() {
        throw Error("sensitive backend credentials");
      },
    },
    async (r) => {
      const response = await r();
      assert.equal(response.status, 503);
      assert.equal((await response.text()).includes("sensitive"), false);
    },
  );
  const app = express();
  app.use(createPrivateSyncRouter({ store: memory(), rateLimit: 2 }));
  const server = app.listen(0);
  await new Promise((r) => server.once("listening", r));
  try {
    const url = `http://127.0.0.1:${server.address().port}`;
    let response = await fetch(url, {
      method: "PUT",
      headers: {
        authorization: "Bearer " + capability,
        "content-type": "application/json",
      },
      body: "{",
    });
    assert.equal(response.status, 400);
    assert.equal(
      (await fetch(url, { headers: { authorization: "Bearer " + capability } }))
        .status,
      200,
    );
    response = await fetch(url, {
      headers: { authorization: "Bearer " + capability },
    });
    assert.equal(response.status, 429);
    assert.equal(response.headers.get("access-control-allow-origin"), null);
  } finally {
    await new Promise((r) => server.close(r));
  }
});
test("Blob adapter refuses missing ETags and recognizes CAS failures", async () => {
  class Conflict extends Error {}
  const sdk = {
    BlobPreconditionFailedError: Conflict,
    async get() {
      return { statusCode: 200, stream: new Blob(["{}"]).stream(), blob: {} };
    },
    async put() {
      throw new Conflict();
    },
  };
  const store = createPrivateSyncStore({ token: "test-only", sdk });
  await assert.rejects(store.read("b".repeat(64)));
  assert.equal(await store.compareAndSwap("b".repeat(64), "old", {}), false);
});
test("same name in a different vault cannot read or mutate the first vault", async () =>
  serve(memory(), async (r) => {
    const a = await (await r("", "PUT", body())).json();
    const other = "NEXOSYNC1." + Buffer.alloc(32, 3).toString("base64url");
    assert.equal(
      (await r("/" + a.item.id, "GET", undefined, other)).status,
      404,
    );
    assert.equal(
      (await r("/" + a.item.id, "DELETE", { expectedRevision: 0 }, other))
        .status,
      409,
    );
    const b = await (await r("", "PUT", body(), other)).json();
    assert.equal(b.item.id, a.item.id);
    assert.equal(b.item.revision, 0);
    assert.equal(
      (await r("/" + b.item.id, "DELETE", { expectedRevision: 0 }, other))
        .status,
      200,
    );
    assert.equal((await r("/" + a.item.id)).status, 200);
  }));
test("parallel distinct names retry ledger CAS without lost entries", async () =>
  serve(memory(), async (r) => {
    const results = await Promise.all(
      Array.from({ length: 6 }, (_, i) => r("", "PUT", body("Parallel " + i))),
    );
    assert.deepEqual(
      results.map((x) => x.status),
      Array(6).fill(201),
    );
    assert.equal((await (await r()).json()).items.length, 6);
  }));
test("private ledger fetch requests identity encoding so CAS receives storage strong ETag", async () => {
  const etag = '"0123456789abcdef0123456789abcdef"';
  let observed;
  const sdk = {
    async get(key, options) {
      observed = options.headers;
      return {
        statusCode: 200,
        stream: new Blob(['{"entries":{}}']).stream(),
        blob: {
          etag:
            options.headers?.["Accept-Encoding"] === "identity"
              ? etag
              : "W/" + etag,
        },
      };
    },
    async put(key, value, options) {
      assert.equal(options.ifMatch, etag);
    },
  };
  const store = createPrivateSyncStore({ storeId: "store_test", sdk });
  const previous = await store.read("c".repeat(64));
  assert.deepEqual(observed, { "Accept-Encoding": "identity" });
  assert.equal(
    await store.compareAndSwap("c".repeat(64), previous.etag, previous.value),
    true,
  );
});
