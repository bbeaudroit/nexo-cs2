"use client";

import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from "react";
import { AnimatePresence, motion, useReducedMotion } from "framer-motion";
import { ArrowRight } from "lucide-react";
import { GlassIconReveal } from "@/components/ui/liquid-glass-logo-reveal";

export const INTRO_SESSION_KEY = "nexo.intro.seen.v1";

export function NexoIntro({ onFinish }: { onFinish: () => void }) {
  const [visible, setVisible] = useState(true);
  const [ready, setReady] = useState(false);
  const reduced = useReducedMotion();
  const skip = useRef<HTMLButtonElement>(null);
  const completion = useRef<ReturnType<typeof setTimeout> | undefined>(
    undefined,
  );
  const finished = useRef(false);

  const finish = useCallback(() => {
    if (finished.current) return;
    finished.current = true;
    try {
      sessionStorage.setItem(INTRO_SESSION_KEY, "1");
    } catch {
      /* Private browsing may disable storage. */
    }
    setVisible(false);
  }, []);

  const scheduleFinish = useCallback(
    (milliseconds: number) => {
      clearTimeout(completion.current);
      completion.current = setTimeout(finish, milliseconds);
    },
    [finish],
  );

  useLayoutEffect(() => {
    const page = document.getElementById("app")!;
    const previousInert = page.inert;
    const previousFocus = document.activeElement as HTMLElement | null;
    page.inert = true;
    document.body.classList.add("intro-active");
    skip.current?.focus({ preventScroll: true });
    return () => {
      clearTimeout(completion.current);
      page.inert = previousInert;
      document.body.classList.remove("intro-active");
      const target =
        previousFocus &&
        previousFocus !== document.body &&
        previousFocus.isConnected
          ? previousFocus
          : page.querySelector<HTMLElement>("#main");
      target?.focus({ preventScroll: true });
    };
  }, []);

  useEffect(() => {
    // A slow GPU or failed asset must never trap visitors behind an intro.
    const safety = setTimeout(finish, 5000);
    if (reduced) scheduleFinish(180);
    return () => clearTimeout(safety);
  }, [finish, reduced, scheduleFinish]);

  return (
    <AnimatePresence onExitComplete={onFinish}>
      {visible && (
        <motion.div
          id="nexo-intro"
          role="dialog"
          aria-modal="true"
          aria-label="Nexo introduction"
          className="nexo-intro"
          initial={false}
          exit={{ opacity: 0 }}
          transition={{ duration: reduced ? 0 : 0.3 }}
          onKeyDown={(event) => {
            if (event.key === "Escape") {
              event.preventDefault();
              finish();
            }
            if (event.key === "Tab") {
              event.preventDefault();
              skip.current?.focus();
            }
          }}
        >
          <div className="intro-identity">
            <div className="intro-visual">
              {!reduced && (
                <GlassIconReveal
                  logo="/favicon.svg"
                  label="Nexo logo"
                  look="light"
                  background="#f0edf4"
                  logoColor="#8e56c4"
                  originalColors
                  loop={false}
                  tileSize={260}
                  logoScale={0.62}
                  finalScale={0.9}
                  speed={1.2}
                  tuning={{ present: 1.5, glide: 1.35, startZoom: 2.3 }}
                  className={`intro-glass${ready ? " is-ready" : ""}`}
                  onReady={() => setReady(true)}
                  onComplete={() => scheduleFinish(240)}
                  onFallback={() => {
                    setReady(false);
                    scheduleFinish(900);
                  }}
                />
              )}
              <img
                className="intro-static-logo"
                src="/favicon.svg"
                width="126"
                height="126"
                alt={reduced ? "Nexo logo" : ""}
                hidden={ready && !reduced}
              />
            </div>
            <span className="intro-wordmark" aria-hidden="true">
              nexo
            </span>
          </div>
          <button
            ref={skip}
            type="button"
            className="intro-skip"
            aria-label="Skip intro"
            title="Skip intro"
            onClick={finish}
          >
            <ArrowRight aria-hidden="true" />
          </button>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
