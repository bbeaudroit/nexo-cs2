import { copyFile, mkdir, access } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const candidates = [
  process.env.NEXO_MENU_ROOT,
  path.resolve(root, "../../desktop-ui"),
  path.resolve(process.cwd(), "desktop-ui"),
].filter(Boolean);
let source;
for (const candidate of candidates) {
  try {
    await access(path.join(candidate, "electron/profile-share.cjs"));
    source = candidate;
    break;
  } catch {}
}
if (!source)
  throw Error(
    "Desktop menu source unavailable. Set NEXO_MENU_ROOT to desktop-ui.",
  );
const destination = path.join(root, "server/config-contract");
await mkdir(path.join(destination, "electron"), { recursive: true });
for (const file of [
  "electron/profile-share.cjs",
  "electron/settings.cjs",
  "defaults.json",
  "skin-catalog.json",
  "electron/skin-loadout.cjs",
])
  await copyFile(path.join(source, file), path.join(destination, file));
console.log("Synchronized desktop config validation contract.");
