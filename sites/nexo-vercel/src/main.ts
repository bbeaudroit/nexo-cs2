import { header, footer, landing, configsPage, dialogs } from "./view";
import { features, shots } from "./menu-content";
import {
  $,
  icon,
  escape,
  refreshIcons,
  openDialog,
  closeDialog,
  initDialogs,
} from "./ui";
import { initWizard } from "./wizard-ui";
import { initCommunity } from "./community-ui";
import menu from "./generated/menu.json";
import { mountEnhancements } from "./enhancements";
import "./tailwind.css";
import "./style.css";
import "./enhancements.css";

const cfgPage = /^\/cfgs\/?$/.test(location.pathname);
let settings = { downloadAvailable: false, requestsAvailable: false };
if (cfgPage) document.title = "CFGs — Nexo";
$("#app").innerHTML =
  header(cfgPage) +
  (cfgPage ? configsPage() : landing()) +
  footer() +
  dialogs();
refreshIcons();
initDialogs();
initWizard();
if (cfgPage) initCommunity();
mountEnhancements(cfgPage);
document.addEventListener("click", (event) => {
  const target = (event.target as Element).closest<HTMLElement>("button");
  if (!target) return;
  if (target.hasAttribute("data-close")) {
    closeDialog(target.closest("dialog")!);
    return;
  }
  if (target.hasAttribute("data-download")) {
    if (settings.downloadAvailable) location.href = "/api/download";
    else openDialog("download-dialog");
  }
  if (target.hasAttribute("data-rebrand")) openDialog("rebrand-dialog");
  if (target.hasAttribute("data-requirements"))
    openDialog("requirements-dialog");
  if (target.dataset.feature) showFeature(target.dataset.feature);
  if (target.dataset.shot) selectShot(target.dataset.shot);
  if (target.dataset.image) showImage(target.dataset.image);
});
function showImage(id: string) {
  const shot = shots.find((s) => s.id === id) || shots[0];
  $<HTMLImageElement>("#large-image").src = "/menu/" + shot.id + ".png";
  $<HTMLImageElement>("#large-image").alt =
    "Actual Nexo " + shot.label + " menu";
  $("#large-caption").textContent = shot.caption;
  openDialog("image-dialog");
}
function showFeature(id: string) {
  const f = features.find((item) => item.id === id);
  if (!f) return;
  const module = menu.modules.find((m) => m.id === f.module),
    tabs = module?.tabs.filter((t) => !f.tab || t.id === f.tab) || [];
  $("#feature-content").innerHTML =
    `<span class="dialog-icon">${icon(f.icon)}</span><h2 id="feature-title">${f.title}</h2><p class="feature-description">${f.description}</p><ul class="plain-list">${f.notes.map((note) => `<li>${note}</li>`).join("")}</ul><button class="feature-shot" data-image="${f.image}" aria-label="Enlarge ${f.title} menu"><img src="/menu/${f.image}.png" width="1040" height="680" alt="Nexo ${f.title} settings" loading="lazy"><span>${icon("expand")}View screenshot</span></button>${f.id === "cfg" ? '<a class="button secondary" href="/cfgs/">Browse CFGs' + icon("arrow-up-right") + "</a>" : ""}<details class="all-controls"><summary>Available controls${icon("plus")}</summary>${tabs.map((tab) => `<h3>${escape(tab.title)}</h3><p>${tab.controls.map((control) => escape(control.label)).join(" · ") || "Saved files and share codes."}</p>`).join("")}</details>`;
  refreshIcons();
  openDialog("feature-dialog");
}
function selectShot(id: string) {
  const shot = shots.find((s) => s.id === id);
  if (!shot) return;
  document
    .querySelectorAll<HTMLElement>("[role=tab][data-shot]")
    .forEach((tab) => {
      tab.setAttribute("aria-selected", String(tab.dataset.shot === id));
      tab.tabIndex = tab.dataset.shot === id ? 0 : -1;
    });
  $<HTMLImageElement>("#gallery-image").src = "/menu/" + id + ".png";
  $<HTMLImageElement>("#gallery-image").alt =
    "Actual Nexo " + shot.label + " menu";
  $("#gallery-caption").textContent = shot.caption;
  $("#gallery-panel").setAttribute("aria-labelledby", "shot-" + id);
  $(".gallery-main").setAttribute("data-image", id);
}
$(".gallery-tabs")?.addEventListener("keydown", (event) => {
  const key = (event as KeyboardEvent).key;
  if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(key)) return;
  const tabs = [
      ...document.querySelectorAll<HTMLElement>("[role=tab][data-shot]"),
    ],
    index = tabs.indexOf(document.activeElement as HTMLElement),
    next =
      key === "Home"
        ? 0
        : key === "End"
          ? tabs.length - 1
          : (index + (key === "ArrowRight" ? 1 : -1) + tabs.length) %
            tabs.length;
  (event as KeyboardEvent).preventDefault();
  selectShot(tabs[next].dataset.shot!);
  tabs[next].focus();
});
fetch("/api/config", { signal: AbortSignal.timeout(10000) })
  .then((r) => (r.ok ? r.json() : Promise.reject()))
  .then((result) => (settings = result))
  .catch(() => {});
