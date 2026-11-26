import { useEffect, useRef, type JSX, type ReactNode } from "react";
import { useLocation } from "react-router";
import { Disclosure } from "@/components/Disclosure";
import { useSettingsPanelState } from "@/components/server/useSettingsPanelState";
import "@/components/server/sections.css";

/** Props of {@link SettingsPanel}. */
export interface SettingsPanelProps {
  /** The anchor id of the panel, for example `server-automation`. The location hash `#<id>` opens the panel. */
  id: string;
  /** The heading text. It is the accessible name of the toggle button and of the panel content. */
  title: ReactNode;
  /** Optional live summary line that stays visible while the panel is collapsed. */
  summary?: ReactNode | undefined;
  /** Optional badge next to the title, for example the staged change count. */
  badge?: ReactNode | undefined;
  /**
   * The keys of the changes staged inside the panel. A non-empty list pins the panel, so Collapse all skips it, and a
   * key that was not in the list before opens the panel. The member can still close it by hand.
   */
  stagedKeys?: readonly string[] | undefined;
  /** Optional extra class on the section, for example `workspace-panel--record`. */
  className?: string | undefined;
  /** The panel body. It stays mounted while the panel is collapsed, so unsaved input survives. */
  children: ReactNode;
}

/**
 * A collapsible panel of the Settings section: a raised `section` with an anchor id whose `h2` sits in the header of
 * a disclosure, followed by a one-line live summary and an optional badge.
 *
 * The open state comes from the surrounding {@link SettingsPanelStateProvider}. The panel opens by itself, and is
 * never closed by itself, when the location hash names it (it then also scrolls below the sticky chrome and focuses
 * its header button on the next frame), and when a change is newly staged inside it. The body stays mounted behind the
 * `hidden` attribute while the panel is collapsed.
 *
 * @param props The anchor id, the heading, the summary, the badge, the staged change keys and the body.
 * @returns The panel.
 */
export function SettingsPanel({
  id,
  title,
  summary,
  badge,
  stagedKeys,
  className,
  children,
}: SettingsPanelProps): JSX.Element {
  const { isOpen, open, toggle, register, setPinned } = useSettingsPanelState();
  const location = useLocation();
  const sectionRef = useRef<HTMLElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const stagedSignature = (stagedKeys ?? []).join("\n");
  const pinned = stagedSignature !== "";
  const seenStaged = useRef<ReadonlySet<string>>(new Set());

  useEffect(() => register(id), [id, register]);

  useEffect(() => {
    setPinned(id, pinned);
    return () => setPinned(id, false);
  }, [id, pinned, setPinned]);

  // A staged change opens the panel the moment it is new; a member who closes it keeps it closed until the next one.
  useEffect(() => {
    const keys = stagedSignature === "" ? [] : stagedSignature.split("\n");
    const known = seenStaged.current;
    seenStaged.current = new Set(keys);
    if (keys.some((key) => !known.has(key))) open(id);
  }, [stagedSignature, id, open]);

  // The location hash opens the panel on every location key change that carries it, then scrolls it below the sticky
  // chrome (the scroll padding of the page) and focuses the header button, not the heading (C-08).
  useEffect(() => {
    if (location.hash !== `#${id}`) return;
    open(id);
    const frame = requestAnimationFrame(() => {
      if (typeof sectionRef.current?.scrollIntoView === "function") {
        sectionRef.current.scrollIntoView({ behavior: "auto" });
      }
      buttonRef.current?.focus();
    });
    return () => cancelAnimationFrame(frame);
  }, [location.key, location.hash, id, open]);

  const classNames = ["workspace-panel", "settings-panel", className]
    .filter((value) => value)
    .join(" ");
  return (
    <section ref={sectionRef} id={id} className={classNames} data-settings-panel={id}>
      <Disclosure
        variant="panel"
        headingLevel={2}
        title={title}
        summary={summary}
        badge={badge}
        open={isOpen(id)}
        onOpenChange={() => toggle(id)}
        buttonRef={buttonRef}
      >
        {children}
      </Disclosure>
    </section>
  );
}
