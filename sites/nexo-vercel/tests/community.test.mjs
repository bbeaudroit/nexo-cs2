import test from "node:test";
import assert from "node:assert/strict";
import express from "express";
import { createRequire } from "node:module";
import { createCommunityRouter } from "../server/community.mjs";
const require = createRequire(import.meta.url);
const {
  encodeConfig,
} = require("../server/config-contract/electron/profile-share.cjs");
const code = encodeConfig({ name: "Test CFG", author: "Tester", settings: {} });
function memory() {
  const data = new Map();
  return {
    async get(k) {
      return data.get(k) ?? null;
    },
    async putIfAbsent(k, v) {
      if (data.has(k)) return false;
      data.set(k, v);
      return true;
    },
    async list(p) {
      return [...data]
        .filter(([k]) => k.startsWith(p))
        .map(([key, value]) => ({ key, value }));
    },
  };
}
async function serve(store, fn) {
  const app = express();
  app.use(express.json({ limit: "120kb" }));
  app.use("/api/cfgs", createCommunityRouter({ store, secret: "test-secret" }));
  const server = app.listen(0);
  await new Promise((r) => server.once("listening", r));
  try {
    await fn(`http://127.0.0.1:${server.address().port}`);
  } finally {
    await new Promise((r) => server.close(r));
  }
}
const post = (url, body, cookie) =>
  fetch(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...(cookie ? { cookie } : {}),
    },
    body: JSON.stringify(body),
  });
test("publishes validated code; list is private; unique copy counts persist", async () =>
  serve(memory(), async (base) => {
    let r = await post(base + "/api/cfgs", { code, tags: ["legit"] });
    assert.equal(r.status, 201);
    const cfg = await r.json();
    assert.equal(cfg.author, "Tester");
    r = await fetch(base + "/api/cfgs");
    const list = await r.json();
    assert.equal(list.cfgs.length, 1);
    assert.equal(list.cfgs[0].code, undefined);
    r = await post(base + `/api/cfgs/${cfg.id}/copy`, {});
    const cookie = r.headers.get("set-cookie").split(";")[0];
    assert.match(r.headers.get("set-cookie"), /HttpOnly/);
    assert.deepEqual(await r.json(), { code, users: 1 });
    r = await post(base + `/api/cfgs/${cfg.id}/copy`, {}, cookie);
    assert.equal((await r.json()).users, 1);
    r = await post(base + `/api/cfgs/${cfg.id}/copy`, {});
    assert.equal((await r.json()).users, 2);
    r = await post(base + "/api/cfgs", { code, tags: ["rage"] });
    assert.equal(r.status, 200);
  }));
test("rejects corrupt codes, unknown settings, invalid tags and cross-site writes", async () =>
  serve(memory(), async (base) => {
    for (const body of [
      { code: code + "x", tags: [] },
      { code, tags: ["fake"] },
      { code, tags: ["legit", "legit"] },
      { code, tags: [], description: "x".repeat(241) },
    ])
      assert.equal((await post(base + "/api/cfgs", body)).status, 400);
    const r = await fetch(base + "/api/cfgs", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Origin: "https://evil.example",
      },
      body: JSON.stringify({ code, tags: [] }),
    });
    assert.equal(r.status, 403);
  }));
test("missing or failed store is honest 503", async () => {
  for (const store of [
    null,
    {
      async list() {
        throw Error("secret");
      },
    },
  ])
    await serve(store, async (base) => {
      const r = await fetch(base + "/api/cfgs");
      assert.equal(r.status, 503);
      assert.doesNotMatch(await r.text(), /secret/);
    });
});
import { createBlobStore } from "../server/blob-store.mjs";
test("blob adapter paginates, uses private immutable writes, handles races", async () => {
  const seen = [];
  class BlobAlreadyExistsError extends Error {}
  const sdk = {
    BlobAlreadyExistsError,
    async list(options) {
      seen.push(options);
      return options.cursor
        ? { blobs: [{ pathname: "copies/a/two.json" }], hasMore: false }
        : {
            blobs: [{ pathname: "copies/a/one.json" }],
            hasMore: true,
            cursor: "next",
          };
    },
    async put(key, value, options) {
      assert.equal(options.access, "private");
      assert.equal(options.allowOverwrite, false);
      assert.equal(options.addRandomSuffix, false);
      throw new BlobAlreadyExistsError();
    },
    async get() {
      return { statusCode: 200, stream: new Response('{"name":"test"}').body };
    },
  };
  const store = createBlobStore({ token: "fake", sdk });
  assert.equal((await store.list("copies/a/")).length, 2);
  assert.equal(seen[1].cursor, "next");
  assert.equal(await store.putIfAbsent("cfgs/x.json", {}), false);
  assert.deepEqual(await store.get("cfgs/x.json"), { name: "test" });
  assert.equal(createBlobStore({ token: "", storeId: "" }), null);
});
import { createHash } from "node:crypto";
import { deflateRawSync } from "node:zlib";
function rawCode(settings) {
  const body = deflateRawSync(
    Buffer.from(
      JSON.stringify({ version: 1, name: "Raw", author: "Tester", settings }),
    ),
  ).toString("base64url");
  return `NEXO1.${body}.${createHash("sha256").update(body).digest("hex").slice(0, 16)}`;
}
test("uses actual desktop checksum, unknown setting and bounded validation", async () =>
  serve(memory(), async (base) => {
    assert.equal(
      (
        await post(base + "/api/cfgs", {
          code: rawCode({ UnknownSetting: true }),
          tags: [],
        })
      ).status,
      400,
    );
    assert.equal(
      (
        await post(base + "/api/cfgs", {
          code: rawCode({ Frames: 99999 }),
          tags: ["low-end"],
        })
      ).status,
      201,
    );
    assert.equal(
      require("../server/config-contract/electron/profile-share.cjs").decodeConfig(
        rawCode({ Frames: 99999 }),
      ).settings.Frames,
      240,
    );
    assert.equal(
      (
        await post(base + "/api/cfgs", {
          code: rawCode({}).replace(/.$/, "z"),
          tags: [],
        })
      ).status,
      400,
    );
  }));
test("failed persistence never returns code; parallel repeats remain unique", async () => {
  const store = memory();
  await serve(store, async (base) => {
    const published = await (
      await post(base + "/api/cfgs", { code, tags: [] })
    ).json();
    const copied = await post(base + `/api/cfgs/${published.id}/copy`, {});
    const cookie = copied.headers.get("set-cookie").split(";")[0];
    const responses = await Promise.all(
      Array.from({ length: 8 }, () =>
        post(base + `/api/cfgs/${published.id}/copy`, {}, cookie),
      ),
    );
    for (const response of responses)
      assert.equal((await response.json()).users, 1);
    store.putIfAbsent = async () => {
      throw Error("private failure");
    };
    const response = await post(
      base + `/api/cfgs/${published.id}/copy`,
      {},
      cookie,
    );
    assert.equal(response.status, 503);
    assert.equal((await response.json()).code, undefined);
    assert.doesNotMatch(
      JSON.stringify(await store.list("copies/")),
      /127\.0\.0\.1|nexo_cfg_visitor/,
    );
  });
});
test("SDK versions without AlreadyExists error verify existing record after write failure", async () => {
  const sdk = {
    async put() {
      throw Error("Vercel Blob: This blob already exists");
    },
    async get() {
      return { statusCode: 200, stream: new Response("{}").body };
    },
  };
  assert.equal(
    await createBlobStore({ token: "fake", sdk }).putIfAbsent(
      "copies/a/b.json",
      {},
    ),
    false,
  );
  sdk.get = async () => null;
  await assert.rejects(
    createBlobStore({ token: "fake", sdk }).putIfAbsent("copies/a/b.json", {}),
  );
});
test("reading copy code never registers a copy or sets an identity cookie", async () => {
  const store = memory();
  await serve(store, async (base) => {
    const cfg = await (
      await post(base + "/api/cfgs", { code, tags: [] })
    ).json();
    for (let i = 0; i < 2; i++) {
      const response = await fetch(base + `/api/cfgs/${cfg.id}/code`);
      assert.equal(response.status, 200);
      assert.deepEqual(await response.json(), { code });
      assert.equal(response.headers.get("set-cookie"), null);
    }
    assert.equal((await store.list("copies/")).length, 0);
    const detail = await (await fetch(base + `/api/cfgs/${cfg.id}`)).json();
    assert.equal(detail.users, 0);
  });
});
test("search and popularity ordering run before the 60-result cap", async () => {
  const store = memory();
  for (let i = 0; i < 65; i++) {
    const id = i.toString(16).padStart(64, "0");
    await store.putIfAbsent(`cfgs/${id}.json`, {
      id,
      name: i === 64 ? "Last searchable" : "Config " + i,
      author: "Tester",
      description: "",
      tags: ["legit"],
      createdAt: new Date(2026, 0, 1, i).toISOString(),
      code,
    });
    if (i === 64) await store.putIfAbsent(`copies/${id}/visitor.json`, {});
  }
  await serve(store, async (base) => {
    const all = await (await fetch(base + "/api/cfgs")).json();
    assert.equal(all.cfgs.length, 60);
    assert.equal(all.cfgs[0].name, "Last searchable");
    const search = await (
      await fetch(base + "/api/cfgs?q=Last&tag=legit")
    ).json();
    assert.equal(search.cfgs.length, 1);
    assert.equal(search.cfgs[0].name, "Last searchable");
  });
});
test("OIDC store identity is passed to every Blob operation without static token", async () => {
  const calls = [];
  const sdk = {
    async get(key, options) {
      calls.push(options);
      return { statusCode: 200, stream: new Response("{}").body };
    },
    async put(key, value, options) {
      calls.push(options);
    },
    async list(options) {
      calls.push(options);
      return { blobs: [], hasMore: false };
    },
  };
  const store = createBlobStore({ token: "", storeId: "store_test", sdk });
  assert.ok(store);
  await store.get("cfgs/test.json");
  await store.putIfAbsent("cfgs/test.json", {});
  await store.list("cfgs/");
  assert.equal(calls.length, 3);
  for (const options of calls) {
    assert.equal(options.storeId, "store_test");
    assert.equal(Object.hasOwn(options, "token"), false);
  }
  assert.equal(createBlobStore({ token: "", storeId: "" }), null);
});

function casSdk() {
  const records = new Map();
  let version = 0;
  const reads = [];
  class BlobPreconditionFailedError extends Error {}
  return {
    reads,
    records,
    BlobPreconditionFailedError,
    async get(key) {
      reads.push(key);
      const entry = records.get(key);
      return entry
        ? {
            statusCode: 200,
            blob: { etag: entry.etag },
            stream: new Response(JSON.stringify(entry.value)).body,
          }
        : null;
    },
    async put(key, value, options) {
      const old = records.get(key);
      if (
        (options.ifMatch && old?.etag !== options.ifMatch) ||
        (!options.allowOverwrite && old)
      )
        throw new BlobPreconditionFailedError();
      records.set(key, { value: JSON.parse(value), etag: String(++version) });
    },
    async list() {
      throw Error("Listing objects is forbidden for catalog reads");
    },
  };
}
test("catalog shards merge concurrent entries, counts never regress, reads never fetch codes", async () => {
  const sdk = casSdk(),
    store = createBlobStore({ token: "fake", sdk });
  const a = {
    id: "a".repeat(64),
    name: "Alpha",
    author: "Tester",
    tags: [],
    description: "",
    createdAt: "2026-01-01",
    users: 0,
    code: "NEVER INDEX",
  };
  const b = { ...a, id: "a" + "b".repeat(63), name: "Beta" };
  await Promise.all([store.index(a), store.index(b)]);
  await Promise.all([
    store.index({ ...a, users: 5 }),
    store.index({ ...a, users: 2 }),
  ]);
  sdk.reads.length = 0;
  const result = await store.catalog();
  assert.equal(result.length, 2);
  assert.equal(result.find((c) => c.id === a.id).users, 5);
  assert.equal(result[0].code, undefined);
  assert.equal(sdk.reads.length, 16);
  assert.ok(sdk.reads.every((key) => key.startsWith("catalog/")));
});
test("HTTP catalog reads metadata only and failed indexing retries preserve one copy", async () => {
  const store = memory(),
    metadata = new Map();
  let failIndex = false;
  store.catalog = async () => [...metadata.values()];
  store.index = async (cfg) => {
    if (failIndex) throw Error("index unavailable");
    metadata.set(cfg.id, cfg);
  };
  await serve(store, async (base) => {
    const published = await (
      await post(base + "/api/cfgs", { code, tags: [] })
    ).json();
    const oldGet = store.get,
      oldList = store.list;
    store.get = async () => {
      throw Error("catalog fetched code");
    };
    store.list = async () => {
      throw Error("catalog counted individual events");
    };
    const listing = await fetch(base + "/api/cfgs");
    assert.equal(listing.status, 200);
    assert.equal((await listing.json()).cfgs.length, 1);
    store.get = oldGet;
    store.list = oldList;
    failIndex = true;
    const failed = await post(base + `/api/cfgs/${published.id}/copy`, {});
    assert.equal(failed.status, 503);
    const cookie = failed.headers.get("set-cookie");
    assert.ok(cookie);
    failIndex = false;
    const retry = await post(
      base + `/api/cfgs/${published.id}/copy`,
      {},
      cookie.split(";")[0],
    );
    assert.equal((await retry.json()).users, 1);
    assert.equal(metadata.get(published.id).users, 1);
  });
});
test("catalog refuses a full shard without overwriting it", async () => {
  const sdk = casSdk(),
    store = createBlobStore({ token: "fake", sdk });
  const entries = {};
  for (let i = 0; i < 1000; i++) {
    const id = "a" + i.toString(16).padStart(63, "0");
    entries[id] = { id, users: 0 };
  }
  sdk.records.set("catalog/a.json", { etag: "seed", value: entries });
  await assert.rejects(
    store.index({ id: "a" + "f".repeat(63), users: 0 }),
    /full/,
  );
  assert.equal(
    Object.keys(sdk.records.get("catalog/a.json").value).length,
    1000,
  );
});
test("publication retries repair a durable code orphan after index failure", async () => {
  const store = memory();
  let fail = true;
  const entries = new Map();
  store.index = async (cfg) => {
    if (fail) throw Error("index unavailable");
    entries.set(cfg.id, cfg);
  };
  store.catalog = async () => [...entries.values()];
  await serve(store, async (base) => {
    assert.equal(
      (await post(base + "/api/cfgs", { code, tags: ["legit"] })).status,
      503,
    );
    assert.equal((await store.list("cfgs/")).length, 1);
    fail = false;
    const response = await post(base + "/api/cfgs", { code, tags: ["rage"] });
    assert.equal(response.status, 200);
    const cfg = await response.json();
    assert.deepEqual(entries.get(cfg.id).tags, ["legit"]);
    assert.equal(entries.size, 1);
  });
});
test("copy response respects a higher durable indexed counter", async () => {
  const store = memory();
  store.index = async (cfg) => ({ ...cfg, users: 7 });
  await serve(store, async (base) => {
    const cfg = await (
      await post(base + "/api/cfgs", { code, tags: [] })
    ).json();
    const response = await post(base + `/api/cfgs/${cfg.id}/copy`, {});
    assert.equal((await response.json()).users, 7);
  });
});
test("catalog fetches uncompressed strong ETags for conditional index updates", async () => {
  const strong = '"0123456789abcdef0123456789abcdef"';
  let optionsSeen;
  const sdk = {
    async get(key, options) {
      optionsSeen = options;
      return {
        statusCode: 200,
        stream: new Blob(["{}"]).stream(),
        blob: {
          etag:
            options.headers?.["Accept-Encoding"] === "identity"
              ? strong
              : "W/" + strong,
        },
      };
    },
    async put(key, value, options) {
      assert.equal(options.ifMatch, strong);
    },
  };
  await createBlobStore({ storeId: "store_test", sdk }).index({
    id: "d".repeat(64),
    name: "Strong ETag",
    users: 0,
  });
  assert.equal(optionsSeen.headers["Accept-Encoding"], "identity");
});
