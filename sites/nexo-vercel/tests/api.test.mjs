import test from "node:test";
import assert from "node:assert/strict";
import { once } from "node:events";
import { createServer } from "node:http";
import {
  createApp,
  readConfig,
  validateRequest,
  discordPayload,
  sendToDiscord,
} from "../server/app.mjs";

const config = {
  webhookUrl: "https://discord.com/api/webhooks/123/test-token",
  downloadUrl: "https://example.com/nexo.zip",
};
const fields = {
  discord: "test.user",
  brandName: "Test brand",
  changes: "Replace the menu branding and logo.",
  aesthetics: "Use blue accents.",
  newFeatures: "Feature A\n\nFeature B",
  removeFeatures: "Old item",
  logoLinks: "https://example.com/logo.png",
  consent: "yes",
};
function data(values = fields) {
  const form = new FormData();
  for (const [key, value] of Object.entries(values)) form.append(key, value);
  return form;
}
async function serve(t, options = {}) {
  const server = createApp({
    config,
    deliver: async () => {},
    ...options,
  }).listen(0, "127.0.0.1");
  await once(server, "listening");
  t.after(
    () =>
      new Promise((resolve) => {
        server.closeAllConnections();
        server.close(resolve);
      }),
  );
  return `http://127.0.0.1:${server.address().port}`;
}

test("price is calculated by the server, ignoring any submitted total", () => {
  assert.equal(validateRequest({ ...fields, total: "0" }).total, 29);
  assert.equal(
    validateRequest({
      ...fields,
      newFeatures: "",
      removeFeatures: "One\nTwo\nThree",
    }).total,
    25,
  );
});
test("empty, excessive and invalid fields are rejected", () => {
  assert.throws(() => validateRequest({ ...fields, discord: "" }), /Discord/);
  assert.throws(() => validateRequest({ ...fields, consent: "" }), /agree/);
  assert.throws(
    () => validateRequest({ ...fields, website: "spam" }),
    /accepted/,
  );
  assert.throws(
    () =>
      validateRequest({
        ...fields,
        newFeatures: Array(21).fill("Feature").join("\n"),
      }),
    /20 lines/,
  );
  assert.throws(
    () => validateRequest({ ...fields, logoLinks: "javascript:alert(1)" }),
    /HTTPS/,
  );
});
test("fake image files are rejected by signature, even with an image MIME type", () => {
  assert.throws(
    () =>
      validateRequest(fields, [
        { mimetype: "image/png", buffer: Buffer.from("<script>") },
      ]),
    /image files/,
  );
});
test("the combined logo budget stays within Vercel's request limit", () => {
  const png = (size) => {
    const buffer = Buffer.alloc(size);
    Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]).copy(buffer);
    return { mimetype: "image/png", buffer };
  };
  assert.throws(
    () =>
      validateRequest(
        fields,
        Array.from({ length: 3 }, () => png(1536 * 1024)),
      ),
    /total 4 MB/,
  );
  assert.equal(
    validateRequest(fields, [png(2 * 1024 * 1024), png(2 * 1024 * 1024)]).total,
    29,
  );
});
test("Vercel entry exports a working HTTP handler without opening a port", async (t) => {
  const { default: handler } = await import("../api/index.mjs");
  const server = createServer(handler).listen(0, "127.0.0.1");
  await once(server, "listening");
  t.after(
    () =>
      new Promise((resolve) => {
        server.closeAllConnections();
        server.close(resolve);
      }),
  );
  const base = `http://127.0.0.1:${server.address().port}`;
  assert.deepEqual(await (await fetch(base + "/api/health")).json(), {
    ok: true,
  });
  const status = await (await fetch(base + "/api/config")).json();
  assert.equal(typeof status.downloadAvailable, "boolean");
  assert.equal(typeof status.requestsAvailable, "boolean");
  const blocked = await fetch(base + "/api/rebranding", {
    method: "POST",
    headers: { origin: "https://another-site.example" },
    body: data(),
  });
  assert.equal(blocked.status, 403);
});
test("configuration rejects unsafe download and non-Discord webhook URLs", () => {
  assert.deepEqual(
    readConfig({
      NEXO_DOWNLOAD_URL: "javascript:alert(1)",
      DISCORD_WEBHOOK_URL: "https://example.com/api/webhooks/123/test",
    }),
    { downloadUrl: "", webhookUrl: "" },
  );
  assert.deepEqual(
    readConfig({
      NEXO_DOWNLOAD_URL: config.downloadUrl,
      DISCORD_WEBHOOK_URL: config.webhookUrl,
    }),
    config,
  );
});
test("health, frontend and configuration routes work without leaking secrets", async (t) => {
  const url = await serve(t);
  assert.equal((await (await fetch(`${url}/api/health`)).json()).ok, true);
  const status = await (await fetch(`${url}/api/config`)).json();
  assert.deepEqual(status, {
    downloadAvailable: true,
    requestsAvailable: true,
  });
  assert.ok(!JSON.stringify(status).includes("test-token"));
  const response = await fetch(url);
  assert.equal(response.status, 200);
  assert.match(await response.text(), /Nexo — CS2/);
  assert.match(
    response.headers.get("content-security-policy"),
    /frame-ancestors 'none'/,
  );
});
test("download redirects only when a real release URL has been configured", async (t) => {
  const url = await serve(t);
  const response = await fetch(`${url}/api/download`, { redirect: "manual" });
  assert.equal(response.status, 302);
  assert.equal(response.headers.get("location"), config.downloadUrl);
});
test("missing integrations return clear errors, never a false success", async (t) => {
  const url = await serve(t, { config: { webhookUrl: "", downloadUrl: "" } });
  assert.equal((await fetch(`${url}/api/download`)).status, 503);
  assert.equal(
    (await fetch(`${url}/api/rebranding`, { method: "POST", body: data() }))
      .status,
    503,
  );
});
test("multipart requests deliver fields and logo attachments and use server pricing", async (t) => {
  let delivered;
  const url = await serve(t, {
    deliver: async (_webhook, request, files) => {
      delivered = { request, files };
    },
  });
  const form = data({ ...fields, total: "1" });
  const png = Buffer.from(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Zl1sAAAAASUVORK5CYII=",
    "base64",
  );
  form.append("logos", new Blob([png], { type: "image/png" }), "brand.png");
  const response = await fetch(`${url}/api/rebranding`, {
    method: "POST",
    body: form,
  });
  assert.equal(response.status, 201);
  assert.equal((await response.json()).total, 29);
  assert.equal(delivered.request.brandName, "Test brand");
  assert.equal(delivered.files.length, 1);
  assert.deepEqual(delivered.files[0].buffer, png);
});
test("delivery failures remain failures", async (t) => {
  const url = await serve(t, {
    deliver: async () => {
      throw new Error("upstream unavailable");
    },
  });
  const response = await fetch(`${url}/api/rebranding`, {
    method: "POST",
    body: data(),
  });
  assert.equal(response.status, 502);
  assert.match((await response.json()).error, /could not be delivered/);
});
test("cross-site submission and repeated requests are blocked", async (t) => {
  const url = await serve(t, { maxRequests: 1 });
  const crossSite = await fetch(`${url}/api/rebranding`, {
    method: "POST",
    headers: { origin: "https://another-site.example" },
    body: data(),
  });
  assert.equal(crossSite.status, 403);
  assert.equal(
    (await fetch(`${url}/api/rebranding`, { method: "POST", body: data() }))
      .status,
    201,
  );
  assert.equal(
    (await fetch(`${url}/api/rebranding`, { method: "POST", body: data() }))
      .status,
    429,
  );
});
test("oversized and invalid attachments fail before delivery", async (t) => {
  const url = await serve(t);
  const form = data();
  form.append(
    "logos",
    new Blob([new Uint8Array(2 * 1024 * 1024 + 1)], { type: "image/png" }),
    "large.png",
  );
  assert.equal(
    (await fetch(`${url}/api/rebranding`, { method: "POST", body: form }))
      .status,
    400,
  );
  const invalid = data();
  invalid.append(
    "logos",
    new Blob(["not a png"], { type: "image/png" }),
    "fake.png",
  );
  assert.equal(
    (await fetch(`${url}/api/rebranding`, { method: "POST", body: invalid }))
      .status,
    400,
  );
});
test("Discord payload disables mentions and preserves complete request details in an attachment", async (t) => {
  const request = validateRequest({
    ...fields,
    changes: "@everyone " + "x".repeat(1400),
  });
  const payload = discordPayload(request, []);
  assert.deepEqual(payload.allowed_mentions.parse, []);
  assert.ok(payload.embeds[0].fields.every((f) => f.value.length <= 1024));
  let outgoing;
  t.mock.method(globalThis, "fetch", async (url, options) => {
    outgoing = { url, options };
    return { ok: true };
  });
  await sendToDiscord(config.webhookUrl, request, []);
  assert.equal(outgoing.url.hostname, "discord.com");
  assert.equal(outgoing.url.searchParams.get("wait"), "true");
  const fullDetails = await outgoing.options.body.get("files[0]").text();
  assert.ok(fullDetails.includes(request.changes));
  assert.ok(fullDetails.includes("https://example.com/logo.png"));
  assert.equal(outgoing.options.redirect, "error");
});
