import { test, expect, type Page } from "@playwright/test";

const introKey = "nexo.intro.seen.v1";
async function alreadyVisited(page: Page) {
  await page.addInitScript((key) => sessionStorage.setItem(key, "1"), introKey);
}

async function ensureNavigationOpen(page: Page) {
  await page.waitForFunction(
    () =>
      document
        .querySelector(".stacking-navbar")
        ?.getAttribute("data-mobile") ===
      String(window.matchMedia("(max-width: 760px)").matches),
  );
  const toggle = page.getByRole("button", { name: /navigation$/ });
  await toggle.focus();
  if ((await toggle.getAttribute("aria-expanded")) === "false")
    await toggle.press("Enter");
  await expect(toggle).toHaveAttribute("aria-expanded", "true");
}

test("first visit shows the Nexo logo, skip restores access, later visits do not block", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto("/");
  await expect(
    page.getByRole("dialog", { name: "Nexo introduction" }),
  ).toBeVisible();
  expect(
    await page.evaluate(() => ({
      inert: document.getElementById("app")!.inert,
      skipFocused:
        document.activeElement?.getAttribute("aria-label") === "Skip intro",
    })),
  ).toEqual({ inert: true, skipFocused: true });
  await page.getByRole("button", { name: "Skip intro" }).press("Enter");
  await expect(page.locator("#nexo-intro")).toHaveCount(0);
  await expect(page.locator("#app")).toHaveJSProperty("inert", false);
  await expect(page.locator("#main")).toBeFocused();
  await page.reload();
  await expect(
    page.getByRole("button", { name: "Expand navigation" }),
  ).toBeVisible();
  await expect(page.locator("#nexo-intro")).toHaveCount(0);
  expect(errors).toEqual([]);
});

test("the intro completes automatically and releases the page", async ({
  page,
}) => {
  await page.goto("/");
  await expect(
    page.getByRole("dialog", { name: "Nexo introduction" }),
  ).toBeVisible();
  await expect(page.locator("#nexo-intro")).toHaveCount(0, { timeout: 8000 });
  await expect(page.locator("#app")).toHaveJSProperty("inert", false);
  expect(
    await page.evaluate((key) => sessionStorage.getItem(key), introKey),
  ).toBe("1");
});

test("reduced motion uses a static mark and exits promptly", async ({
  page,
}) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/");
  await expect(page.locator(".intro-glass canvas")).toHaveCount(0);
  await expect(page.locator("#nexo-intro")).toHaveCount(0, { timeout: 2000 });
  await expect(page.locator("#app")).toHaveJSProperty("inert", false);
});

test("unavailable WebGL falls back without trapping visitors", async ({
  page,
}) => {
  await page.addInitScript(() => {
    const original = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = function (type, ...args) {
      if (type === "webgl2") return null;
      return original.call(this, type, ...args);
    } as typeof original;
  });
  await page.goto("/");
  await expect(page.locator(".intro-static-logo")).toBeVisible();
  await expect(page.locator("#nexo-intro")).toHaveCount(0, { timeout: 2500 });
  await expect(page.locator("#app")).toHaveJSProperty("inert", false);
});

test("stacking header opens from keyboard or touch, closes with Escape, and keeps CFG navigation", async ({
  page,
  isMobile,
}) => {
  await alreadyVisited(page);
  await page.goto("/");
  const open = page.getByRole("button", { name: "Expand navigation" });
  if (isMobile) await open.click();
  else {
    await open.focus();
    await open.press("Enter");
  }
  const nav = page.getByRole("navigation", { name: "Main navigation" });
  await expect(
    nav.getByRole("link", { name: "The menu", exact: true }),
  ).toBeVisible();
  await nav.getByRole("link", { name: "Features", exact: true }).focus();
  await page.keyboard.press("Escape");
  await expect(
    page.getByRole("button", { name: "Expand navigation" }),
  ).toBeFocused();
  await ensureNavigationOpen(page);
  await nav.getByRole("link", { name: "CFGs", exact: true }).click();
  await expect(page).toHaveURL(/\/cfgs\/$/);
  await expect(
    page.getByRole("heading", { name: "CFGs", exact: true }),
  ).toBeVisible();
  await expect(page.locator("#nexo-intro")).toHaveCount(0);
  await ensureNavigationOpen(page);
  await expect(
    page.locator('.stacking-navbar a[href="/cfgs/"]'),
  ).toHaveAttribute("aria-current", "page");
});

test("the header keeps download behavior and no overflow at small widths", async ({
  page,
  isMobile,
}) => {
  await alreadyVisited(page);
  await page.route("**/api/config", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        downloadAvailable: false,
        requestsAvailable: false,
      }),
    }),
  );
  await page.goto("/");
  await page
    .getByRole("button", { name: "Download Nexo", exact: true })
    .click();
  await expect(
    page.getByRole("dialog", { name: "Download", exact: true }),
  ).toBeVisible();
  await page.keyboard.press("Escape");
  for (const width of isMobile ? [320, 390, 600] : [760, 800, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await ensureNavigationOpen(page);
    await expect(
      page.getByRole("button", { name: "Collapse navigation" }),
    ).toBeVisible();
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth > innerWidth,
      ),
    ).toBe(false);
    await page.keyboard.press("Escape");
  }
  if (!isMobile) {
    await ensureNavigationOpen(page);
    await page.setViewportSize({ width: 390, height: 844 });
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth > innerWidth,
      ),
    ).toBe(false);
    await ensureNavigationOpen(page);
    const bounds = await page.locator(".stacking-item a").evaluateAll((items) =>
      items.map((item) => {
        const b = item.getBoundingClientRect();
        return b.left >= 0 && b.right <= innerWidth;
      }),
    );
    expect(bounds).toEqual([true, true, true]);
  }
});
