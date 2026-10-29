import express from "express";
import { createHash } from "node:crypto";
import { createRequire } from "node:module";
const { decodeConfig } = createRequire(import.meta.url)(
  "./config-contract/electron/profile-share.cjs",
);
const hash = (value) => createHash("sha256").update(value).digest("hex");
const metadata = ({ id, name, author, revision, updatedAt }) => ({
  id,
  name,
  author,
  revision,
  updatedAt,
});
const fail = (status, message) => {
  throw Object.assign(Error(message), { status });
};
export function createPrivateSyncRouter({
  store = null,
  now = () => Date.now(),
  rateLimit = 120,
} = {}) {
  const router = express.Router(),
    limits = new Map();
  router.use((req, res, next) => {
    res.set({
      "Cache-Control": "no-store",
      Vary: "Authorization",
      "Referrer-Policy": "no-referrer",
    });
    const match = /^Bearer (NEXOSYNC1\.([A-Za-z0-9_-]{43}))$/.exec(
      req.get("authorization") || "",
    );
    if (
      !match ||
      Buffer.from(match[2], "base64url").toString("base64url") !== match[2]
    )
      return res
        .status(401)
        .json({ error: "Private sync capability required." });
    req.syncNamespace = hash("Nexo private CFG sync v1\0" + match[1]);
    if (req.get("sec-fetch-site") === "cross-site")
      return res.status(403).json({ error: "Cross-site request rejected." });
    if (req.get("origin")) {
      try {
        if (new URL(req.get("origin")).host !== req.get("host"))
          return res
            .status(403)
            .json({ error: "Cross-site request rejected." });
      } catch {
        return res.status(403).json({ error: "Invalid origin." });
      }
    }
    const time = now();
    for (const [key, slot] of limits)
      if (slot.until <= time) limits.delete(key);
    // Both IP and capability quotas; bounded process-local abuse control.
    for (const key of [
      "ip:" + hash(req.ip || ""),
      "cap:" + req.syncNamespace,
    ]) {
      let slot = limits.get(key);
      if (!slot) {
        if (limits.size >= 10000)
          return res.status(429).json({ error: "Try again shortly." });
        slot = { until: time + 60000, count: 0 };
        limits.set(key, slot);
      }
      if (++slot.count > rateLimit)
        return res
          .status(429)
          .set("Retry-After", "60")
          .json({ error: "Too many requests." });
    }
    if (!store)
      return res
        .status(503)
        .json({ error: "Private sync storage unavailable." });
    if (
      req.body !== undefined &&
      Buffer.byteLength(JSON.stringify(req.body)) > 102400
    )
      return res.status(413).json({ error: "Request too large." });
    next();
  });
  router.use(express.json({ limit: 102400 }));
  const run = (fn) => async (req, res) => {
    try {
      await fn(req, res);
    } catch (error) {
      res.status(error.status || 503).json({
        error: error.status
          ? error.message
          : "Private sync storage unavailable.",
      });
    }
  };
  async function read(req) {
    const previous = await store.read(req.syncNamespace);
    const ledger = previous?.value || { entries: {} };
    if (
      !ledger.entries ||
      typeof ledger.entries !== "object" ||
      Array.isArray(ledger.entries) ||
      Object.keys(ledger.entries).length > 50
    )
      throw Error("Invalid ledger");
    return { previous, ledger };
  }
  router.get(
    "/",
    run(async (req, res) => {
      const { ledger } = await read(req);
      res.json({
        items: Object.values(ledger.entries)
          .filter((x) => !x.deleted)
          .map(metadata),
      });
    }),
  );
  router.get(
    "/:id",
    run(async (req, res) => {
      if (!/^[a-f0-9]{64}$/.test(req.params.id)) fail(404, "CFG not found.");
      const { ledger } = await read(req),
        entry = ledger.entries[req.params.id];
      if (!entry || entry.deleted) fail(404, "CFG not found.");
      decodeConfig(entry.code);
      res.json({ item: metadata(entry), code: entry.code });
    }),
  );
  async function mutate(req, res, deleting) {
    const body = req.body;
    if (
      !body ||
      typeof body !== "object" ||
      Array.isArray(body) ||
      !Object.hasOwn(body, "expectedRevision") ||
      (body.expectedRevision !== null &&
        (!Number.isSafeInteger(body.expectedRevision) ||
          body.expectedRevision < 0))
    )
      fail(400, "Invalid expectedRevision.");
    let decoded, id, code;
    if (deleting) {
      id = req.params.id;
      if (!/^[a-f0-9]{64}$/.test(id) || body.expectedRevision === null)
        fail(400, "Invalid deletion.");
    } else {
      if (typeof body.code !== "string") fail(400, "Invalid NEXO1 code.");
      try {
        decoded = decodeConfig(body.code);
      } catch {
        fail(400, "Invalid NEXO1 code.");
      }
      code = body.code.trim();
      id = hash(
        decoded.name.normalize("NFC").trim().replace(/ +/g, " ").toLowerCase(),
      );
    }
    for (let attempt = 0; attempt < 8; attempt++) {
      const { previous, ledger } = await read(req),
        current = ledger.entries[id];
      if ((current?.revision ?? null) !== body.expectedRevision)
        fail(409, "Revision conflict. Refresh before saving.");
      if (deleting && (!current || current.deleted))
        fail(404, "CFG not found.");
      if (!current && Object.keys(ledger.entries).length >= 50)
        fail(507, "Private sync limit reached (50 config identities).");
      const revision = current ? current.revision + 1 : 0;
      if (!Number.isSafeInteger(revision)) fail(409, "Revision limit reached.");
      const entry = deleting
        ? {
            ...metadata(current),
            revision,
            updatedAt: new Date(now()).toISOString(),
            deleted: true,
          }
        : {
            id,
            name: decoded.name,
            author: decoded.author,
            code,
            revision,
            updatedAt: new Date(now()).toISOString(),
          };
      ledger.entries[id] = entry;
      if (previous && !previous.etag) throw Error("ETag required");
      if (
        await store.compareAndSwap(
          req.syncNamespace,
          previous?.etag ?? null,
          ledger,
        )
      ) {
        res.status(current ? 200 : 201).json({
          item: metadata(entry),
          ...(deleting ? { deleted: true } : {}),
        });
        return;
      }
    }
    fail(409, "Concurrent sync conflict. Refresh and retry.");
  }
  router.put(
    "/",
    run((req, res) => mutate(req, res, false)),
  );
  router.delete(
    "/:id",
    run((req, res) => mutate(req, res, true)),
  );
  router.use((error, req, res, next) =>
    res.status(error.type === "entity.too.large" ? 413 : 400).json({
      error:
        error.type === "entity.too.large"
          ? "Request too large."
          : "Invalid JSON body.",
    }),
  );
  return router;
}
