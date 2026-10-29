import express from "express";
import {
  createHash,
  createHmac,
  randomBytes,
  timingSafeEqual,
} from "node:crypto";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const {
  decodeConfig,
} = require("./config-contract/electron/profile-share.cjs");
const tagsAllowed = new Set([
  "legit",
  "rage",
  "rifles",
  "pistols",
  "snipers",
  "visuals",
  "movement",
  "low-end",
]);
const hash = (value) => createHash("sha256").update(value).digest("hex");
const publicCfg = ({ code, ...cfg }) => cfg;
export function createCommunityRouter({
  store = null,
  secret = process.env.CFG_VISITOR_SECRET || process.env.BLOB_READ_WRITE_TOKEN,
  production = process.env.NODE_ENV === "production",
} = {}) {
  const router = express.Router();
  const limits = new Map();
  router.use((req, res, next) => {
    res.set("Cache-Control", "no-store");
    if (req.method === "POST") {
      if (req.get("sec-fetch-site") === "cross-site")
        return res.status(403).json({ error: "Cross-site request rejected." });
      const origin = req.get("origin");
      if (origin) {
        try {
          if (new URL(origin).host !== req.get("host"))
            return res
              .status(403)
              .json({ error: "Cross-site request rejected." });
        } catch {
          return res.status(403).json({ error: "Invalid origin." });
        }
      }
      const key = hash(req.ip || "unknown");
      const now = Date.now();
      for (const [k, v] of limits) if (v.until < now) limits.delete(k);
      let slot = limits.get(key);
      if (!slot) {
        slot = { until: now + 60000, count: 0 };
        limits.set(key, slot);
      }
      if (++slot.count > 60)
        return res
          .status(429)
          .json({ error: "Too many requests. Try again shortly." });
    }
    if (!store)
      return res.status(503).json({ error: "CFG storage is not configured." });
    next();
  });
  const run = (fn) => async (req, res, next) => {
    try {
      await fn(req, res);
    } catch (error) {
      if (error.status)
        return res.status(error.status).json({ error: error.message });
      res
        .status(503)
        .json({ error: "CFG storage is temporarily unavailable." });
    }
  };
  const invalid = (message) => {
    throw Object.assign(Error(message), { status: 400 });
  };
  async function records() {
    if (store.catalog) return store.catalog();
    return (await store.list("cfgs/")).map((item) => item.value);
  }
  async function users(id) {
    return (await store.list(`copies/${id}/`)).length;
  }
  async function find(req, res) {
    if (!/^[a-f0-9]{64}$/.test(req.params.id)) {
      res.status(404).json({ error: "CFG not found." });
      return null;
    }
    const cfg = await store.get(`cfgs/${req.params.id}.json`);
    if (!cfg) {
      res.status(404).json({ error: "CFG not found." });
      return null;
    }
    return cfg;
  }
  router.get(
    "/",
    run(async (req, res) => {
      const { q = "", tag = "", sort = "popular" } = req.query;
      if (
        typeof q !== "string" ||
        q.length > 80 ||
        typeof tag !== "string" ||
        (tag && !tagsAllowed.has(tag)) ||
        !["popular", "newest"].includes(sort)
      )
        invalid("Invalid search.");
      let cfgs = (await records()).filter(
        (c) =>
          (!tag || c.tags.includes(tag)) &&
          `${c.name} ${c.author} ${c.description}`
            .toLocaleLowerCase()
            .includes(q.toLocaleLowerCase()),
      );
      cfgs = await Promise.all(
        cfgs.map(async (c) => ({
          ...publicCfg(c),
          users: store.catalog ? c.users : await users(c.id),
        })),
      );
      cfgs.sort(
        sort === "newest"
          ? (a, b) => b.createdAt.localeCompare(a.createdAt)
          : (a, b) =>
              b.users - a.users || b.createdAt.localeCompare(a.createdAt),
      );
      res.json({ cfgs: cfgs.slice(0, 60) });
    }),
  );
  router.post(
    "/",
    run(async (req, res) => {
      const { code, tags, description = "" } = req.body || {};
      if (
        typeof code !== "string" ||
        code.length > 100000 ||
        !Array.isArray(tags) ||
        tags.length > 4 ||
        new Set(tags).size !== tags.length ||
        tags.some((t) => !tagsAllowed.has(t)) ||
        typeof description !== "string" ||
        description.length > 240 ||
        /[\x00-\x08\x0b\x0c\x0e-\x1f\x7f<>]/u.test(description)
      )
        invalid("Invalid CFG, tags or description.");
      let decoded;
      try {
        decoded = decodeConfig(code);
      } catch {
        invalid("Invalid or corrupt NEXO1 config code.");
      }
      const normalized = code.trim(),
        id = hash(normalized);
      const cfg = {
        id,
        code: normalized,
        name: decoded.name,
        author: decoded.author,
        tags,
        description: description.trim(),
        createdAt: new Date().toISOString(),
      };
      const created = await store.putIfAbsent(`cfgs/${id}.json`, cfg);
      const saved = created ? cfg : await store.get(`cfgs/${id}.json`);
      let count = await users(id);
      if (store.index)
        count =
          (await store.index({ ...publicCfg(saved), users: count }))?.users ??
          count;
      res
        .status(created ? 201 : 200)
        .json({ ...publicCfg(saved), users: count });
    }),
  );
  router.get(
    "/:id",
    run(async (req, res) => {
      const cfg = await find(req, res);
      if (cfg) res.json({ ...publicCfg(cfg), users: await users(cfg.id) });
    }),
  );
  router.get(
    "/:id/code",
    run(async (req, res) => {
      const cfg = await find(req, res);
      if (cfg) res.json({ code: cfg.code });
    }),
  );
  router.post(
    "/:id/copy",
    run(async (req, res) => {
      const cfg = await find(req, res);
      if (!cfg) return;
      if (!secret)
        return res
          .status(503)
          .json({ error: "CFG visitor identity is not configured." });
      const sign = (id) =>
        createHmac("sha256", secret).update(id).digest("hex");
      const cookie = (req.get("cookie") || "")
        .split(";")
        .map((v) => v.trim())
        .find((v) => v.startsWith("nexo_cfg_visitor="))
        ?.slice(17);
      let visitor;
      const match = /^([a-f0-9]{64})\.([a-f0-9]{64})$/.exec(cookie || "");
      if (
        match &&
        timingSafeEqual(Buffer.from(match[2]), Buffer.from(sign(match[1])))
      )
        visitor = match[1];
      else visitor = randomBytes(32).toString("hex");
      const visitorHash = createHmac("sha256", secret)
        .update("cfg-copy:" + visitor)
        .digest("hex");
      // Keep identity on failure so retrying an already persisted event is unique.
      res.cookie("nexo_cfg_visitor", `${visitor}.${sign(visitor)}`, {
        httpOnly: true,
        sameSite: "lax",
        secure: production,
        maxAge: 31536000000,
        path: "/api/cfgs",
      });
      await store.putIfAbsent(`copies/${cfg.id}/${visitorHash}.json`, {
        copiedAt: new Date().toISOString(),
      });
      let count = await users(cfg.id);
      if (store.index)
        count =
          (await store.index({ ...publicCfg(cfg), users: count }))?.users ??
          count;
      res.json({ code: cfg.code, users: count });
    }),
  );
  return router;
}
