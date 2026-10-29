import {
  $,
  icon,
  escape,
  refreshIcons,
  formMessage,
  openDialog,
  closeDialog,
} from "./ui";
import { createCopyTracker } from "./copy-tracker";
type Cfg = {
  id: string;
  name: string;
  author: string;
  tags: string[];
  description: string;
  createdAt: string;
  users: number;
};
export function initCommunity() {
  const track = createCopyTracker(
    navigator.locks
      ? async (job) => {
          await navigator.locks.request("nexo-cfg-copy", job);
        }
      : undefined,
  );
  let loadSequence = 0;
  async function load() {
    const seq = ++loadSequence,
      form = $<HTMLFormElement>("#cfg-filters"),
      query = new URLSearchParams(
        new FormData(form) as unknown as Record<string, string>,
      );
    $("#cfg-results").innerHTML =
      '<div class="empty-state">Loading CFGs…</div>';
    try {
      const response = await fetch("/api/cfgs?" + query, {
          signal: AbortSignal.timeout(15000),
        }),
        result = await response.json();
      if (seq !== loadSequence) return;
      if (!response.ok)
        throw Error(result.error || "CFGs could not be loaded.");
      const list: Cfg[] = result.cfgs;
      $("#cfg-count").textContent =
        list.length + " CFG" + (list.length === 1 ? "" : "s");
      $("#cfg-results").innerHTML = list.length
        ? list
            .map(
              (cfg) =>
                `<article class="cfg-card"><div class="cfg-card-top"><span class="cfg-file-icon">${icon("sliders-horizontal")}</span><span class="cfg-author">by ${escape(cfg.author)}</span></div><h2>${escape(cfg.name)}</h2><p>${escape(cfg.description || "A shared Nexo setup.")}</p><div class="cfg-tags">${cfg.tags.map((tag) => `<span>${escape(tag)}</span>`).join("")}</div><div class="cfg-card-footer"><span class="user-count" title="Unique browsers that copied this CFG">${icon("users")}<b data-users="${cfg.id}">${cfg.users}</b> <span data-user-label="${cfg.id}">${cfg.users === 1 ? "user" : "users"}</span></span><button class="button small secondary" data-copy-cfg="${cfg.id}" aria-label="Copy ${escape(cfg.name)}">${icon("copy")}Copy code</button></div><p class="copy-status" data-copy-status="${cfg.id}" role="status" hidden></p></article>`,
            )
            .join("")
        : '<div class="empty-state"><span class="dialog-icon">' +
          icon("sliders-horizontal") +
          "</span><h2>No CFGs here yet</h2><p>Try another search, or share the first one.</p></div>";
      refreshIcons();
    } catch (error) {
      if (seq !== loadSequence) return;
      $("#cfg-count").textContent = "";
      $("#cfg-results").innerHTML =
        `<div class="empty-state"><span class="dialog-icon">${icon("sliders-horizontal")}</span><h2>CFGs are not available right now</h2><p>${escape(error instanceof Error ? error.message : "Please try again later.")}</p><button class="button secondary" id="cfg-retry">Try again</button></div>`;
      refreshIcons();
      $("#cfg-retry")?.addEventListener("click", load);
    }
  }
  async function copy(button: HTMLButtonElement, id: string) {
    button.disabled = true;
    const status = $(`[data-copy-status="${id}"]`);
    status.hidden = false;
    status.textContent = "Copying…";
    try {
      const response = await fetch("/api/cfgs/" + id + "/code", {
          signal: AbortSignal.timeout(15000),
        }),
        result = await response.json();
      if (!response.ok) throw Error(result.error || "Code unavailable.");
      await navigator.clipboard.writeText(result.code);
      status.textContent =
        "Code copied. Paste it in Nexo → Configs → Share code.";
      button.innerHTML = icon("check") + "Copied";
      refreshIcons();
      try {
        await track(async () => {
          const tracked = await fetch("/api/cfgs/" + id + "/copy", {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: "{}",
              signal: AbortSignal.timeout(15000),
            }),
            counter = await tracked.json();
          if (!tracked.ok) throw Error();
          $(`[data-users="${id}"]`).textContent = String(counter.users);
          $(`[data-user-label="${id}"]`).textContent =
            counter.users === 1 ? "user" : "users";
        });
      } catch {
        status.textContent += " The counter could not update.";
      }
    } catch (error) {
      status.textContent =
        error instanceof Error
          ? error.message
          : "Could not copy. Please allow clipboard access and try again.";
      button.innerHTML = icon("copy") + "Try again";
      refreshIcons();
    } finally {
      button.disabled = false;
    }
  }
  $("#counts-help").addEventListener("click", () =>
    openDialog("counts-dialog"),
  );
  $("#publish-open").addEventListener("click", () =>
    openDialog("publish-dialog"),
  );
  let timer: ReturnType<typeof setTimeout>;
  $("#cfg-filters").addEventListener("input", () => {
    clearTimeout(timer);
    timer = setTimeout(load, 220);
  });
  $("#cfg-filters").addEventListener("submit", (event) => {
    event.preventDefault();
    clearTimeout(timer);
    load();
  });
  $("#cfg-results").addEventListener("click", (event) => {
    const button = (event.target as Element).closest<HTMLButtonElement>(
      "[data-copy-cfg]",
    );
    if (button) copy(button, button.dataset.copyCfg!);
  });
  const form = $<HTMLFormElement>("#publish-form");
  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    const data = new FormData(form),
      tags = data.getAll("tags");
    if (tags.length > 4) {
      formMessage("#publish-status", "Choose up to 4 tags.");
      return;
    }
    const button = form.querySelector<HTMLButtonElement>(
      "button[type=submit]",
    )!;
    button.disabled = true;
    formMessage("#publish-status", "Sharing…", false);
    try {
      const response = await fetch("/api/cfgs", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            code: String(data.get("code") || "").trim(),
            description: data.get("description") || "",
            tags,
          }),
          signal: AbortSignal.timeout(15000),
        }),
        result = await response.json();
      if (!response.ok) throw Error(result.error || "Could not share the CFG.");
      form.reset();
      formMessage("#publish-status", "");
      closeDialog($<HTMLDialogElement>("#publish-dialog"));
      load();
    } catch (error) {
      formMessage(
        "#publish-status",
        error instanceof Error
          ? error.message
          : "Could not share. Your code is still here.",
      );
    } finally {
      button.disabled = false;
    }
  });
  load();
}
