import { quote, validateStep } from "./rebranding";
import { $, icon, escape, refreshIcons, formMessage } from "./ui";
export function initWizard() {
  const form = $<HTMLFormElement>("#rebrand-form");
  let step = 0,
    sending = false;
  const values = () =>
    Object.fromEntries(
      [...new FormData(form)].filter(([, v]) => typeof v === "string"),
    ) as Record<string, string>;
  const logos = () =>
    (form.elements.namedItem("logos") as HTMLInputElement).files;
  function render() {
    document
      .querySelectorAll<HTMLElement>("[data-step]")
      .forEach((f) => (f.hidden = Number(f.dataset.step) !== step));
    document.querySelectorAll(".wizard-steps li").forEach((item, i) => {
      item.toggleAttribute("data-complete", i < step);
      if (i === step) item.setAttribute("aria-current", "step");
      else item.removeAttribute("aria-current");
    });
    $("#wizard-back").hidden = step === 0;
    $("#wizard-next").innerHTML =
      step === 2
        ? "Send request" + icon("arrow-up-right")
        : "Next" + icon("arrow-right");
    if (step === 2) {
      const v = values();
      $("#rebrand-review").innerHTML =
        `<dl><div><dt>Your brand</dt><dd>${escape(v.brandName)}</dd></div><div><dt>Discord</dt><dd>${escape(v.discord)}</dd></div><div><dt>Your idea</dt><dd>${escape(v.changes)}</dd></div><div><dt>Additions</dt><dd>${escape(v.newFeatures?.trim() || "None")}</dd></div><div><dt>Removals</dt><dd>${escape(v.removeFeatures?.trim() || "None")}</dd></div><div><dt>Visual changes</dt><dd>${escape(v.aesthetics?.trim() || "None")}</dd></div><div><dt>Logos</dt><dd>${logos()?.length || 0} files</dd></div></dl>`;
    }
    refreshIcons();
  }
  form.addEventListener("input", () => {
    $("#wizard-total").innerHTML =
      "$" + quote(values().newFeatures || "") + " <small>USD</small>";
  });
  $("#wizard-back").addEventListener("click", () => {
    if (sending) return;
    step = Math.max(0, step - 1);
    formMessage("#rebrand-status", "");
    render();
  });
  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (sending) return;
    formMessage("#rebrand-status", "");
    const v = values(),
      invalid = validateStep(step, v);
    if (invalid) {
      formMessage("#rebrand-status", invalid.message);
      form.querySelector<HTMLElement>(`[name=${invalid.field}]`)?.focus();
      return;
    }
    const files = [...(logos() || [])];
    if (
      files.length > 3 ||
      files.some((f) => f.size > 2 * 1024 * 1024) ||
      files.reduce((n, f) => n + f.size, 0) > 4 * 1024 * 1024
    ) {
      formMessage(
        "#rebrand-status",
        "Choose up to 3 logos, 2 MB each and 4 MB total.",
      );
      return;
    }
    if (step < 2) {
      step++;
      render();
      form
        .querySelector<HTMLElement>(
          `[data-step="${step}"] input,[data-step="${step}"] textarea`,
        )
        ?.focus();
      return;
    }
    if (v.consent !== "yes") {
      formMessage(
        "#rebrand-status",
        "Please agree to send the details to Discord.",
      );
      return;
    }
    sending = true;
    $<HTMLButtonElement>("#wizard-next").disabled = true;
    $<HTMLButtonElement>("#wizard-back").disabled = true;
    formMessage("#rebrand-status", "Sending…", false);
    try {
      const response = await fetch("/api/rebranding", {
          method: "POST",
          body: new FormData(form),
          signal: AbortSignal.timeout(30000),
        }),
        result = await response.json();
      if (!response.ok)
        throw Error(result.error || "Could not send the request.");
      formMessage(
        "#rebrand-status",
        "Request sent. We will contact you on Discord.",
        false,
      );
      form.querySelectorAll("fieldset").forEach((f) => (f.hidden = true));
      $("#wizard-back").hidden = true;
      $("#wizard-next").hidden = true;
    } catch (error) {
      formMessage(
        "#rebrand-status",
        error instanceof Error
          ? error.message
          : "Could not send. Your entries are still here.",
      );
    } finally {
      sending = false;
      $<HTMLButtonElement>("#wizard-next").disabled = false;
      $<HTMLButtonElement>("#wizard-back").disabled = false;
    }
  });
}
