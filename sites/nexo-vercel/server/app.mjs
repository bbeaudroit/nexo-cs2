import express from "express";
import { createPrivateSyncRouter } from "./private-sync.mjs";
import { createPrivateSyncStore } from "./private-sync-store.mjs";
import multer from "multer";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createHash } from "node:crypto";
import { createCommunityRouter } from "./community.mjs";
import { createBlobStore } from "./blob-store.mjs";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const upload = multer({
  storage: multer.memoryStorage(),
  limits: {
    fileSize: 2 * 1024 * 1024,
    files: 3,
    fields: 12,
    fieldSize: 8000,
    parts: 15,
  },
});

export function readConfig(env = process.env) {
  const download = env.NEXO_DOWNLOAD_URL?.trim() || "";
  const webhook = env.DISCORD_WEBHOOK_URL?.trim() || "";
  const validDownload =
    download &&
    (() => {
      try {
        const u = new URL(download);
        return u.protocol === "https:" && !u.username && !u.password;
      } catch {
        return false;
      }
    })();
  // A fixed Discord host prevents the form from becoming an arbitrary request proxy.
  const validWebhook =
    /^https:\/\/(?:discord\.com|discordapp\.com)\/api(?:\/v\d+)?\/webhooks\/\d+\/[A-Za-z0-9_-]+\/?$/.test(
      webhook,
    );
  return {
    downloadUrl: validDownload ? download : "",
    webhookUrl: validWebhook ? webhook : "",
  };
}

function lines(value, name, max = 20) {
  const items = String(value || "")
    .split(/\r?\n/)
    .map((s) => s.trim())
    .filter(Boolean);
  if (items.length > max || items.some((s) => s.length > 180)) {
    throw new Error(
      `${name}: use at most ${max} lines, with 180 characters per line.`,
    );
  }
  return items;
}

function textField(body, key, label, min, max) {
  const value = typeof body[key] === "string" ? body[key].trim() : "";
  if (value.length < min || value.length > max)
    throw new Error(`${label} must contain ${min}–${max} characters.`);
  return value;
}

export function validateRequest(body, files = []) {
  // Keep the complete multipart request below Vercel's 4.5 MB payload limit.
  // The remaining budget covers text fields and multipart boundaries.
  if (
    files.reduce((size, file) => size + file.buffer.length, 0) >
    4 * 1024 * 1024
  )
    throw new Error("Logo files must total 4 MB or less.");
  if (body.website) throw new Error("Request could not be accepted.");
  const discord = textField(body, "discord", "Discord username", 2, 50);
  const brandName = textField(body, "brandName", "Rebrand name", 2, 80);
  const changes = textField(body, "changes", "Requested changes", 10, 1500);
  const aesthetics = textField(body, "aesthetics", "Visual changes", 0, 1000);
  const newFeatures = lines(body.newFeatures, "New features");
  const removeFeatures = lines(body.removeFeatures, "Removals");
  const logoLinks = lines(body.logoLinks, "Logo links", 3);
  for (const link of logoLinks) {
    try {
      const u = new URL(link);
      if (u.protocol !== "https:" || u.username || u.password)
        throw new Error();
    } catch {
      throw new Error("Logo links must be valid HTTPS URLs.");
    }
  }
  if (body.consent !== "yes")
    throw new Error(
      "Please agree to send these details to the team on Discord.",
    );
  for (const file of files) {
    const b = file.buffer;
    const isPng =
      b.length >= 8 &&
      b.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]));
    const isJpg =
      b.length >= 3 && b[0] === 0xff && b[1] === 0xd8 && b[2] === 0xff;
    const isWebp =
      b.length >= 12 &&
      b.toString("ascii", 0, 4) === "RIFF" &&
      b.toString("ascii", 8, 12) === "WEBP";
    const valid =
      (file.mimetype === "image/png" && isPng) ||
      (file.mimetype === "image/jpeg" && isJpg) ||
      (file.mimetype === "image/webp" && isWebp);
    if (!valid) throw new Error("Logos must be PNG, JPG or WebP image files.");
  }
  return {
    discord,
    brandName,
    changes,
    aesthetics,
    newFeatures,
    removeFeatures,
    logoLinks,
    total: 25 + newFeatures.length * 2,
  };
}

function safeDiscord(value) {
  // Block mentions and neutralize user-controlled Markdown in the message.
  return value.replaceAll("@", "@\u200b").replace(/[\\`*_~|>\[\]]/g, "\\$&");
}

export function discordPayload(data, files) {
  const safe = safeDiscord;
  const numbered = (items) =>
    items.length
      ? items.map((s, i) => `${i + 1}. ${safe(s)}`).join("\n")
      : "None";
  // Each Discord embed field has a hard 1024-character limit. Preserve all details
  // in a text attachment rather than silently truncating larger submissions.
  const summary = {
    username: "Nexo requests",
    allowed_mentions: { parse: [], users: [], roles: [] },
    embeds: [
      {
        title: "New rebranding request",
        color: 0xaaf5e5,
        fields: [
          { name: "Discord", value: safe(data.discord), inline: true },
          { name: "Rebrand name", value: safe(data.brandName), inline: true },
          {
            name: "Estimate (USD)",
            value: `$25 + ${data.newFeatures.length} × $2 = $${data.total}. Removals: free.`,
            inline: false,
          },
          {
            name: "Requested changes",
            value: safe(data.changes).slice(0, 1000),
            inline: false,
          },
          {
            name: "Visual changes",
            value: safe(data.aesthetics || "None specified").slice(0, 1000),
            inline: false,
          },
          {
            name: "Added features",
            value: numbered(data.newFeatures).slice(0, 1000),
            inline: false,
          },
          {
            name: "Removed features",
            value: numbered(data.removeFeatures).slice(0, 1000),
            inline: false,
          },
          {
            name: "Logos",
            value: `${files.length} image attachment(s). Full details in request.txt.`,
            inline: false,
          },
        ],
        footer: {
          text: "Request only · No payment collected · Scope requires review",
        },
        timestamp: new Date().toISOString(),
      },
    ],
  };
  return summary;
}

export async function sendToDiscord(webhookUrl, data, files) {
  const form = new FormData();
  const payload = discordPayload(data, files);
  const allDetails = [
    `Discord: ${data.discord}`,
    `Rebrand: ${data.brandName}`,
    `Estimate: $${data.total} USD`,
    `\nRequested changes:\n${data.changes}`,
    `\nVisual changes:\n${data.aesthetics || "None"}`,
    `\nNew features ($2 each):\n${data.newFeatures.join("\n") || "None"}`,
    `\nRemovals (free):\n${data.removeFeatures.join("\n") || "None"}`,
    `\nLogo links:\n${data.logoLinks.join("\n") || "None"}`,
  ].join("\n");
  form.append(
    "files[0]",
    new Blob([allDetails], { type: "text/plain" }),
    "request.txt",
  );
  files.forEach((file, i) => {
    const extension = {
      "image/png": "png",
      "image/jpeg": "jpg",
      "image/webp": "webp",
    }[file.mimetype];
    form.append(
      `files[${i + 1}]`,
      new Blob([file.buffer], { type: file.mimetype }),
      `logo-${i + 1}.${extension}`,
    );
  });
  form.append("payload_json", JSON.stringify(payload));
  const url = new URL(webhookUrl);
  url.searchParams.set("wait", "true");
  const response = await fetch(url, {
    method: "POST",
    body: form,
    signal: AbortSignal.timeout(15000),
    redirect: "error",
  });
  if (!response.ok) {
    // Never include the webhook URL, token, or upstream response body in logs/errors.
    throw new Error(`Discord delivery failed (${response.status}).`);
  }
}

export function createApp({
  config = readConfig(),
  deliver = sendToDiscord,
  maxRequests = 5,
  communityStore = createBlobStore(),
  privateSyncStore = createPrivateSyncStore(),
  communitySecret = process.env.CFG_VISITOR_SECRET ||
    process.env.BLOB_READ_WRITE_TOKEN ||
    (process.env.DISCORD_WEBHOOK_URL
      ? createHash("sha256")
          .update("nexo-cfg-cookie:v1:" + process.env.DISCORD_WEBHOOK_URL)
          .digest("hex")
      : undefined),
} = {}) {
  const app = express();
  const attempts = new Map();
  app.disable("x-powered-by");
  app.use((req, res, next) => {
    res.set({
      "X-Content-Type-Options": "nosniff",
      "Referrer-Policy": "strict-origin-when-cross-origin",
      "X-Frame-Options": "DENY",
      "Cache-Control": req.path.startsWith("/api") ? "no-store" : "no-cache",
      "Content-Security-Policy":
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'",
    });
    next();
  });
  app.use("/api/sync", createPrivateSyncRouter({store:privateSyncStore}));
  app.get("/api/health", (_req, res) => res.json({ ok: true }));
  app.use(
    "/api/cfgs",
    express.json({ limit: "120kb" }),
    createCommunityRouter({
      store: communityStore,
      secret: communitySecret,
      production:
        process.env.VERCEL === "1" || process.env.NODE_ENV === "production",
    }),
  );
  app.get("/api/config", (_req, res) =>
    res.json({
      downloadAvailable: !!config.downloadUrl,
      requestsAvailable: !!config.webhookUrl,
    }),
  );
  app.get("/api/download", (_req, res) => {
    if (!config.downloadUrl)
      return res
        .status(503)
        .json({ error: "The download file has not been published yet." });
    res.redirect(302, config.downloadUrl);
  });
  app.post(
    "/api/rebranding",
    (req, res, next) => {
      const origin = req.get("origin");
      try {
        if (
          req.get("sec-fetch-site") === "cross-site" ||
          (origin && new URL(origin).host !== req.get("host"))
        ) {
          return res
            .status(403)
            .json({ error: "Submit this form from the Nexo website." });
        }
      } catch {
        return res.status(403).json({ error: "Invalid request origin." });
      }
      const now = Date.now();
      for (const [key, value] of attempts)
        if (value.reset < now) attempts.delete(key);
      const key = req.ip;
      const previous = attempts.get(key) || { count: 0, reset: now + 600000 };
      if (previous.count >= maxRequests)
        return res.status(429).set("Retry-After", "600").json({
          error: "Too many requests. Please try again in 10 minutes.",
        });
      previous.count++;
      attempts.set(key, previous);
      if (!config.webhookUrl)
        return res.status(503).json({
          error:
            "Requests are temporarily unavailable. Your form has been kept. Please try again later.",
        });
      next();
    },
    upload.array("logos", 3),
    async (req, res) => {
      let data;
      try {
        data = validateRequest(req.body, req.files);
      } catch (error) {
        return res.status(400).json({ error: error.message });
      }
      try {
        await deliver(config.webhookUrl, data, req.files || []);
        return res.status(201).json({
          ok: true,
          total: data.total,
          message:
            "Request sent. The team will contact you on Discord to confirm the scope.",
        });
      } catch {
        return res.status(502).json({
          error:
            "Your request could not be delivered. Your form has been kept. Please try again later.",
        });
      }
    },
  );
  app.use("/api", (_req, res) =>
    res.status(404).json({ error: "Endpoint not found." }),
  );
  app.get(["/cfgs", "/cfgs/", "/en", "/en/"], (_req, res) =>
    res.sendFile(path.join(root, "dist", "index.html")),
  );
  app.use(express.static(path.join(root, "dist"), { index: "index.html" }));
  app.use((error, _req, res, _next) => {
    if (error instanceof multer.MulterError)
      return res.status(400).json({
        error:
          "Use up to 3 logo images, 2 MB each. Text fields must stay within their limits.",
      });
    res.status(500).json({ error: "The request could not be processed." });
  });
  return app;
}
