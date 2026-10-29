"use client";

import { useEffect, useRef, useState, type CSSProperties } from "react";
import { motion, useReducedMotion } from "framer-motion";
import {
  Grid2X2,
  Images,
  Layers,
  SlidersHorizontal,
  X,
  type LucideIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";

export interface StackingNavItem {
  href: string;
  label: string;
  icon: LucideIcon;
  current?: boolean;
}

export interface StackingNavbarProps {
  items?: StackingNavItem[];
  className?: string;
}

const defaultItems: StackingNavItem[] = [
  { href: "/#features", label: "Features", icon: Grid2X2 },
  { href: "/#menu", label: "The menu", icon: Images },
  { href: "/cfgs/", label: "CFGs", icon: SlidersHorizontal },
];

/** The supplied 21st.dev stacking pattern, adapted to Vite's native links. */
export function StackingNavbar({
  items = defaultItems,
  className,
}: StackingNavbarProps) {
  const [hovered, setHovered] = useState(false);
  const [focused, setFocused] = useState(false);
  const [pinned, setPinned] = useState(false);
  const [mobile, setMobile] = useState(
    () => window.matchMedia("(max-width: 760px)").matches,
  );
  const reduced = useReducedMotion();
  const toggle = useRef<HTMLButtonElement>(null);
  const nav = useRef<HTMLElement>(null);
  const expanded = hovered || focused || pinned;

  useEffect(() => {
    const media = window.matchMedia("(max-width: 760px)");
    const update = () => {
      setMobile(media.matches);
      setHovered(false);
      setFocused(false);
      setPinned(false);
    };
    media.addEventListener("change", update);
    return () => media.removeEventListener("change", update);
  }, []);

  useEffect(() => {
    if (!expanded) return;
    const closeOutside = (event: PointerEvent) => {
      if (!nav.current?.contains(event.target as Node)) {
        setPinned(false);
        setFocused(false);
        setHovered(false);
      }
    };
    document.addEventListener("pointerdown", closeOutside);
    return () => document.removeEventListener("pointerdown", closeOutside);
  }, [expanded]);

  function collapse() {
    setHovered(false);
    setFocused(false);
    setPinned(false);
  }

  return (
    <nav
      ref={nav}
      aria-label="Main navigation"
      className={cn("stacking-navbar", className)}
      data-expanded={expanded}
      data-mobile={mobile}
      onPointerEnter={(event) => {
        if (!mobile && event.pointerType === "mouse") setHovered(true);
      }}
      onPointerLeave={() => setHovered(false)}
      onBlurCapture={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget)) {
          setFocused(false);
          setPinned(false);
        }
      }}
      onKeyDown={(event) => {
        if (event.key === "Escape") {
          event.preventDefault();
          toggle.current?.focus();
          collapse();
        }
      }}
    >
      <button
        ref={toggle}
        type="button"
        className="stacking-toggle"
        aria-label={expanded ? "Collapse navigation" : "Expand navigation"}
        aria-expanded={expanded}
        aria-controls="stacking-nav-items"
        onClick={() => {
          const next = !expanded;
          setHovered(false);
          setFocused(false);
          setPinned(next);
        }}
      >
        {expanded ? <X aria-hidden="true" /> : <Layers aria-hidden="true" />}
      </button>
      <motion.div
        id="stacking-nav-items"
        className="stacking-items"
        data-open={expanded}
        initial={false}
        animate={
          mobile
            ? {}
            : {
                width: expanded
                  ? items.length * 126 - 8
                  : 118 + (items.length - 1) * 11,
              }
        }
        transition={
          reduced
            ? { duration: 0 }
            : { type: "spring", stiffness: 250, damping: 28 }
        }
      >
        {items.map((item, index) => (
          <StackingNavbarItem
            key={item.href}
            item={item}
            expanded={expanded}
            mobile={mobile}
            index={index}
            onFocus={() => setFocused(true)}
            onNavigate={collapse}
          />
        ))}
      </motion.div>
    </nav>
  );
}

function StackingNavbarItem({
  item,
  expanded,
  mobile,
  index,
  onFocus,
  onNavigate,
}: {
  item: StackingNavItem;
  expanded: boolean;
  mobile: boolean;
  index: number;
  onFocus: () => void;
  onNavigate: () => void;
  style?: CSSProperties;
}) {
  const reduced = useReducedMotion();
  const Icon = item.icon;
  return (
    <motion.div
      className="stacking-item"
      initial={false}
      animate={{ x: mobile ? 0 : expanded ? 126 * index : 11 * index }}
      transition={
        reduced
          ? { duration: 0 }
          : { duration: 0.6, delay: 0.06 * index, type: "spring", bounce: 0.12 }
      }
      style={{ zIndex: 100 - index }}
      aria-hidden={!expanded && (mobile || index > 0) ? true : undefined}
    >
      <a
        href={item.href}
        className="flex items-center justify-center gap-2 rounded-3xl no-underline backdrop-blur-lg transition-colors duration-300"
        aria-current={item.current ? "page" : undefined}
        tabIndex={!expanded && (mobile || index > 0) ? -1 : 0}
        onFocus={onFocus}
        onClick={onNavigate}
      >
        <Icon aria-hidden="true" />
        <span>{item.label}</span>
      </a>
    </motion.div>
  );
}
