import { Add16Regular, Subtract16Regular } from "@fluentui/react-icons";
import { useId, useState } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { Disclosure } from "@/components/Disclosure";
import { ApiText } from "@/components/server/ApiText";
import "@/components/server/mods.css";
import type { ModPackComparison, ModPackItem } from "@/features/hosting/modPackDiff";

/** How many mods a group lists before the member asks for all of them. */
const previewSize = 10;

/** Props of one group of the diff. */
interface DiffGroupProps {
  /** The visible heading with the count. */
  heading: string;
  /** The mods of the group. */
  items: readonly ModPackItem[];
  /** The marker that tells added mods from removed and unchanged ones. */
  tone: "added" | "removed" | "unchanged";
  /** Whether the list starts collapsed behind its count; the member can then only expand it. */
  collapsed: boolean;
}

/**
 * One group of the contents diff: a label-style heading with the count, then a semantic list.
 *
 * Added and removed groups list their first mods and offer Show all; an unchanged group starts collapsed behind its
 * count. The count in the heading is always visible. Titles come from the API and wrap, never ellipsized.
 *
 * @param props The heading, the mods, the tone and whether the list starts collapsed.
 */
function DiffGroup({ heading, items, tone, collapsed }: DiffGroupProps) {
  const { t } = useTranslation();
  const headingId = useId();
  const [expanded, setExpanded] = useState(false);
  const preview = collapsed ? 0 : previewSize;
  const visible = expanded ? items : items.slice(0, preview);
  const canToggle = items.length > preview;
  const Marker = tone === "removed" ? Subtract16Regular : Add16Regular;
  return (
    <div className="mod-diff__group">
      <p className="mod-diff__heading" id={headingId}>
        {heading}
      </p>
      {visible.length > 0 && (
        <ul className="mod-diff__list" aria-labelledby={headingId}>
          {visible.map((item, index) => (
            <li key={`${item.key}:${index}`} className="mod-diff__entry">
              {tone !== "unchanged" && (
                <Marker
                  className={`mod-diff__marker mod-diff__marker--${tone}`}
                  aria-hidden="true"
                />
              )}
              <ApiText>{item.title}</ApiText>
            </li>
          ))}
        </ul>
      )}
      {canToggle && (
        <ActionButton
          variant="ghost"
          size="compact"
          className="mod-diff__toggle"
          aria-expanded={expanded}
          onClick={() => setExpanded((current) => !current)}
        >
          {expanded
            ? t("Server_Mods_Diff_ShowFewer")
            : t("Server_Mods_Diff_ShowAll", { count: items.length })}
        </ActionButton>
      )}
    </div>
  );
}

/**
 * The contents diff of a mod pack change, rendered below the mod pack row of the review.
 *
 * It tells the member which mods the new pack adds and removes. While the contents load it says so; when the
 * contents failed or the API sends none it only says that the comparison is not possible and the change can still
 * be applied (the two titles above stay the only information); equal contents get one sentence. Otherwise the
 * Added, Removed and Unchanged groups follow, each with its count; a group without mods is left out. The groups sit
 * in an inline disclosure titled with the number of mod changes (added plus removed) that starts closed, so the row
 * of the review stays one line until the member asks for the contents.
 *
 * @param props.comparison The comparison state from `useModPackComparison`.
 * @returns The diff, or null while no mod pack change is staged.
 */
export function ModPackDiff({ comparison }: { comparison: ModPackComparison }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const { status, diff } = comparison;
  if (status === "idle") return null;
  if (status === "loading")
    return <p className="mod-diff__note">{t("Server_Change_Blocked_Comparing")}</p>;
  if (status === "failed") return <p className="mod-diff__note">{t("Server_Mods_Diff_Failed")}</p>;
  if (status === "unknown")
    return <p className="mod-diff__note">{t("Server_Mods_Contents_Unknown")}</p>;
  if (diff.added.length === 0 && diff.removed.length === 0)
    return <p className="mod-diff__note">{t("Server_Mods_Diff_Same")}</p>;
  return (
    <Disclosure
      className="mod-diff__disclosure"
      title={t("Server_Change_ModDiff_Toggle", { count: diff.added.length + diff.removed.length })}
      open={open}
      onOpenChange={setOpen}
    >
      <div className="mod-diff">
        {diff.added.length > 0 && (
          <DiffGroup
            heading={t("Server_Mods_Diff_Added", { count: diff.added.length })}
            items={diff.added}
            tone="added"
            collapsed={false}
          />
        )}
        {diff.removed.length > 0 && (
          <DiffGroup
            heading={t("Server_Mods_Diff_Removed", { count: diff.removed.length })}
            items={diff.removed}
            tone="removed"
            collapsed={false}
          />
        )}
        {diff.unchanged.length > 0 && (
          <DiffGroup
            heading={t("Server_Mods_Diff_Unchanged", { count: diff.unchanged.length })}
            items={diff.unchanged}
            tone="unchanged"
            collapsed
          />
        )}
      </div>
    </Disclosure>
  );
}
