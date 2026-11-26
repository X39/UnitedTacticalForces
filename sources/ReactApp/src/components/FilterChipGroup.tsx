import { Checkmark16Regular } from "@fluentui/react-icons";
import { ActionButton } from "@/components/ActionControl";
import { currentLocale } from "@/app/format";
import "@/components/server/fleet-toolbar.css";

/**
 * One chip of a {@link FilterChipGroup}.
 *
 * @typeParam TId The string union of chip identifiers.
 */
export interface FilterChipItem<TId extends string> {
  /** Stable identifier reported to `onChange` when the chip is activated. */
  id: TId;
  /** Visible chip label. */
  label: string;
  /** Optional count shown after the label. Zero is shown, never hidden. */
  count?: number;
}

/**
 * Props of {@link FilterChipGroup}.
 *
 * @typeParam TId The string union of chip identifiers.
 */
export interface FilterChipGroupProps<TId extends string> {
  /** Accessible name of the group. */
  label: string;
  /** Chips in display order. */
  items: readonly FilterChipItem<TId>[];
  /** Identifier of the single pressed chip. */
  value: TId;
  /**
   * Called with the identifier of an activated chip, including the chip that is already pressed.
   *
   * @param id The identifier of the activated chip.
   */
  onChange(id: TId): void;
  /**
   * Formats a chip count. Defaults to locale number formatting for the current UI language.
   *
   * @param count The count to format.
   * @returns The display text of the count.
   */
  formatCount?: (count: number) => string;
}

/**
 * Formats a count with the number format of the current UI language.
 *
 * @param count The count to format.
 * @returns The locale formatted count.
 */
function formatLocaleCount(count: number): string {
  return new Intl.NumberFormat(currentLocale()).format(count);
}

/**
 * A shared group of toggle chips of which exactly one is pressed.
 *
 * Each chip is a real button with `aria-pressed`, reachable through the normal Tab order; arrow keys are not
 * intercepted. The pressed state is carried by a check icon and a stronger border, never by colour alone. Chips are
 * never disabled, so a chip with a zero count stays selectable.
 *
 * @typeParam TId The string union of chip identifiers.
 */
export function FilterChipGroup<TId extends string>({
  label,
  items,
  value,
  onChange,
  formatCount = formatLocaleCount,
}: FilterChipGroupProps<TId>) {
  return (
    <div role="group" aria-label={label} className="filter-chip-group">
      {items.map((item) => {
        const pressed = item.id === value;
        return (
          <ActionButton
            key={item.id}
            variant="ghost"
            className="filter-chip"
            aria-pressed={pressed}
            onClick={() => onChange(item.id)}
          >
            {pressed && <Checkmark16Regular className="filter-chip__check" aria-hidden="true" />}
            <span className="filter-chip__label">{item.label}</span>
            {item.count !== undefined && (
              <>
                {" "}
                <span className="filter-chip__count">{formatCount(item.count)}</span>
              </>
            )}
          </ActionButton>
        );
      })}
    </div>
  );
}
