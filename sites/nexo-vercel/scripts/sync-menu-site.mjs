import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath, pathToFileURL } from "node:url";
const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), ".."),
  root = path.resolve(repo, "../.."),
  source = path.join(root, "desktop-ui/src/catalog.mjs");
const output = path.join(repo, "src/generated/menu.json");
if (!fs.existsSync(source)) {
  if (!fs.existsSync(output))
    throw Error("Menu snapshot missing. Run sync:menu in F:\\CS2.");
  console.log("Using committed menu snapshot.");
  process.exit(0);
}
const { catalog } = await import(pathToFileURL(source).href);
const clean = (s) =>
  s.replace(/configuration/gi, "CFG").replace(/capture/gi, "Streamproof");
const modules = catalog.map((page) => ({
  id: page.id,
  title: page.label === "Configs" ? "CFG" : page.label,
  tabs: page.tabs.map((tab) => ({
    id: tab.id,
    title: tab.label === "Capture" ? "Streamproof" : tab.label,
    controls: tab.rows.map((row) => ({
      key: row.key,
      label: clean(row.label),
    })),
  })),
}));
const links=JSON.parse(fs.readFileSync(path.join(root,'desktop-ui/social-links.json'),'utf8'));
const version=JSON.parse(fs.readFileSync(path.join(root,"desktop-ui/package.json"),"utf8")).version;
const files = [
  path.join(root,"desktop-ui/package.json"),
  path.join(root, "desktop-ui/defaults.json"),
  source,
  ...fs
    .readdirSync(path.join(root, "esp"))
    .filter((f) => f.endsWith(".cs"))
    .sort()
    .map((f) => path.join(root, "esp", f)),
];
const hash = crypto.createHash("sha256");
function walk(folder){return fs.readdirSync(folder,{withFileTypes:true}).sort((a,b)=>a.name.localeCompare(b.name)).flatMap(entry=>entry.isDirectory()?walk(path.join(folder,entry.name)):[path.join(folder,entry.name)]);}
files.push(...walk(path.join(root,'desktop-ui/src')),...walk(path.join(root,'desktop-ui/electron')),...walk(path.join(root,'desktop-ui/assets')),path.join(root,'desktop-ui/social-links.json'));
for (const f of [...new Set(files)].sort()) hash.update(path.relative(root,f)).update('\0').update(fs.readFileSync(f));
const json =
  JSON.stringify(
  { version, sourceHash: hash.digest("hex"), modules, links },
    null,
    2,
  ) + "\n";
if (process.argv.includes("--check")) {
  if (!fs.existsSync(output) || fs.readFileSync(output, "utf8") !== json)
    throw Error(
      "Menu changed: run npm run sync:menu and review website feature descriptions/screenshots.",
    );
  console.log("Menu and website snapshot match.");
} else {
  fs.mkdirSync(path.dirname(output), { recursive: true });
  fs.writeFileSync(output, json);
  console.log("Website menu snapshot synchronized.");
}
