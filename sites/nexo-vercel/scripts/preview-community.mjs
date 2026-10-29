// Loopback-only QA fixture. Production never imports this module.
import { createApp } from "../server/app.mjs";
import { createRequire } from "node:module";
import { createHash } from "node:crypto";
const require = createRequire(import.meta.url),
  {
    encodeConfig,
  } = require("../server/config-contract/electron/profile-share.cjs"),
  { defaults } = require("../server/config-contract/electron/settings.cjs");
const records = new Map();
const store = {
  async get(key) {
    return records.get(key) || null;
  },
  async putIfAbsent(key, value) {
    if (records.has(key)) return false;
    records.set(key, value);
    return true;
  },
  async list(prefix) {
    return [...records]
      .filter(([key]) => key.startsWith(prefix))
      .map(([key, value]) => ({ key, value }));
  },
};
for (const [name, tags, description, settings] of [
  [
    "Practice · Preview",
    ["legit", "rifles"],
    "Local test CFG. Not published.",
    { ...defaults.settings },
  ],
  [
    "Visuals · Preview",
    ["visuals", "low-end"],
    "Local test CFG with visuals on.",
    { ...defaults.settings, Enabled: true, Boxes: true },
  ],
  [
    "Movement · Preview",
    ["movement"],
    "Local test CFG for movement controls.",
    { ...defaults.settings, BunnyHop: true },
  ],
]) {
  const code = encodeConfig({ name, author: "Preview", settings }),
    id = createHash("sha256").update(code).digest("hex");
  await store.putIfAbsent("cfgs/" + id + ".json", {
    id,
    code,
    name,
    author: "Preview",
    tags,
    description,
    createdAt: new Date().toISOString(),
  });
}
const app = createApp({
  communityStore: store,
  communitySecret: "local-preview-only-not-a-production-secret",
  config: { downloadUrl: "", webhookUrl: "" },
});
app.listen(4179, "127.0.0.1", () =>
  console.log(
    "Loopback QA preview :4179. Synthetic CFGs; no Discord delivery.",
  ),
);
