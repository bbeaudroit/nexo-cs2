import { test, expect } from "@playwright/test";

// Intro behavior has dedicated tests; keep these existing feature/wizard flows focused.
test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => sessionStorage.setItem("nexo.intro.seen.v1", "1"));
});

test("simple landing, clickable features and actual menu gallery", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto("/");
  await expect(page.getByRole("heading", { level: 1 })).toHaveText(
    "Nexofor CS2.",
  );
  await expect(page.locator(".feature-card")).toHaveCount(8);
  await expect(
    page.getByRole("heading", { name: "A welcome of your own." }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("heading", { name: "Your setup, in a code." }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("heading", { name: "Verification", exact: true }),
  ).toHaveCount(0);
  await page.locator("[data-feature=radar]").click();
  await expect(
    page.getByRole("dialog", { name: "Radar", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText("The shared radar shows the full map without rotating."),
  ).toBeVisible();
  await page.keyboard.press("Escape");
  await page.getByRole("tab", { name: "Movement", exact: true }).click();
  await expect(page.locator("#gallery-image")).toHaveAttribute(
    "src",
    "/menu/movement.png",
  );
  await page
    .getByRole("button", {
      name: "Enlarge selected menu screenshot",
      exact: true,
    })
    .click();
  await expect(page.locator("#image-dialog")).toBeVisible();
  await page.keyboard.press("Escape");
  await page.locator(".hero-actions [data-download]").click();
  await expect(
    page.getByRole("dialog", { name: "Download", exact: true }),
  ).toBeVisible();
  expect(errors).toEqual([]);
});
test("wizard preserves entries when going back and calculates only additions", async ({
  page,
}) => {
  await page.goto("/");
  await page.locator(".hero-actions [data-rebrand]").click();
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await expect(page.locator("#rebrand-status")).toContainText(
    "Discord username",
  );
  await page
    .getByRole("textbox", { name: "Discord username", exact: true })
    .fill("preview.user");
  await page
    .getByRole("textbox", { name: "Brand name", exact: true })
    .fill("Nova");
  await page
    .getByRole("textbox", { name: "Your idea", exact: true })
    .fill("A blue menu with a new logo.");
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await page.locator("[name=newFeatures]").fill("One\n\nTwo");
  await page.locator("[name=removeFeatures]").fill("Radar");
  await expect(page.locator("#wizard-total")).toHaveText("$29 USD");
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await expect(page.locator("#rebrand-review")).toContainText("Nova");
  await page.getByRole("button", { name: "Back", exact: true }).click();
  await expect(page.locator("[name=newFeatures]")).toHaveValue("One\n\nTwo");
});
test("CFGs are a separate page with tags and honest unavailable state", async ({
  page,
}) => {
  await page.route("**/api/cfgs?**", (route) =>
    route.fulfill({
      status: 503,
      contentType: "application/json",
      body: JSON.stringify({
        error: "CFG storage is temporarily unavailable.",
      }),
    }),
  );
  await page.goto("/cfgs/");
  await expect(
    page.getByRole("heading", { name: "CFGs", exact: true }),
  ).toBeVisible();
  await expect(page.locator(".hero")).toHaveCount(0);
  await expect(
    page.getByRole("combobox", { name: "Filter by tag" }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "CFGs are not available right now" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Share a CFG", exact: true }).click();
  await expect(
    page.getByRole("dialog", { name: "Share a CFG", exact: true }),
  ).toBeVisible();
});
test("landing and CFGs fit without horizontal overflow", async ({ page }) => {
  for (const route of ["/", "/cfgs/"]) {
    await page.goto(route);
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth > innerWidth,
      ),
    ).toBe(false);
  }
});
