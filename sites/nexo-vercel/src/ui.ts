import {
  createIcons,
  ArrowUpRight,
  ArrowRight,
  ArrowLeft,
  Download,
  Palette,
  Eye,
  Crosshair,
  MousePointer2,
  Radar,
  Layers,
  Footprints,
  SlidersHorizontal,
  ScanEye,
  X,
  Plus,
  Copy,
  Search,
  Check,
  Users,
  Expand,
  CircleHelp,
} from "lucide";
const icons = {
  ArrowUpRight,
  ArrowRight,
  ArrowLeft,
  Download,
  Palette,
  Eye,
  Crosshair,
  MousePointer2,
  Radar,
  Layers,
  Footprints,
  SlidersHorizontal,
  ScanEye,
  X,
  Plus,
  Copy,
  Search,
  Check,
  Users,
  Expand,
  CircleHelp,
};
export const icon = (name: string) =>
  `<i data-lucide="${name}" aria-hidden="true"></i>`;
export const escape = (value: unknown) =>
  String(value ?? "").replace(
    /[&<>"']/g,
    (c) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[
        c
      ]!,
  );
export const $ = <T extends Element = HTMLElement>(selector: string) =>
  document.querySelector<T>(selector)!;
export const refreshIcons = () =>
  createIcons({ icons, attrs: { "stroke-width": 1.7 } });
const openers = new WeakMap<HTMLDialogElement, HTMLElement>();
export function openDialog(id: string) {
  const dialog = $<HTMLDialogElement>("#" + id);
  openers.set(dialog, document.activeElement as HTMLElement);
  dialog.showModal();
  document.body.classList.add("dialog-open");
}
export function closeDialog(dialog: HTMLDialogElement) {
  dialog.close();
}
export function initDialogs() {
  document.querySelectorAll<HTMLDialogElement>("dialog").forEach((dialog) => {
    dialog.addEventListener("click", (event) => {
      if (event.target === dialog) closeDialog(dialog);
    });
    dialog.addEventListener("close", () => {
      document.body.classList.toggle(
        "dialog-open",
        !!document.querySelector("dialog[open]"),
      );
      openers.get(dialog)?.focus();
    });
  });
}
export function formMessage(selector: string, message: string, error = true) {
  const node = $(selector);
  node.textContent = message;
  node.hidden = !message;
  node.classList.toggle("error", error);
}
