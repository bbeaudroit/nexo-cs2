import { createRoot } from "react-dom/client";
import { flushSync } from "react-dom";
import { MotionConfig } from "framer-motion";
import { Grid2X2, Images, SlidersHorizontal } from "lucide-react";
import { StackingNavbar } from "@/components/ui/stacking-navbar";
import { INTRO_SESSION_KEY, NexoIntro } from "@/components/ui/nexo-intro";

export function mountEnhancements(cfgPage: boolean) {
  const header = document.getElementById("stacking-nav-root");
  if (header)
    createRoot(header).render(
      <MotionConfig reducedMotion="user">
        <StackingNavbar
          items={[
            { href: "/#features", label: "Features", icon: Grid2X2 },
            { href: "/#menu", label: "The menu", icon: Images },
            {
              href: "/cfgs/",
              label: "CFGs",
              icon: SlidersHorizontal,
              current: cfgPage,
            },
          ]}
        />
      </MotionConfig>,
    );

  let seen = false;
  try {
    seen = sessionStorage.getItem(INTRO_SESSION_KEY) === "1";
  } catch {
    /* Storage is optional. */
  }
  if (cfgPage || seen) return;
  const container = document.createElement("div");
  container.id = "intro-root";
  document.body.append(container);
  const intro = createRoot(container);
  flushSync(() =>
    intro.render(
      <MotionConfig reducedMotion="user">
        <NexoIntro
          onFinish={() => {
            // Wait until AnimatePresence finishes its exit before releasing the root.
            queueMicrotask(() => {
              intro.unmount();
              container.remove();
            });
          }}
        />
      </MotionConfig>,
    ),
  );
}
