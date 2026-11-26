import { Dismiss20Regular, Search20Regular } from "@fluentui/react-icons";
import { useId, useRef, type KeyboardEvent, type RefObject } from "react";
import { ActionButton } from "@/components/ActionControl";
import "@/components/server/fleet-toolbar.css";

/** Props of {@link SearchField}. */
export interface SearchFieldProps {
  /** Accessible name of the input. Rendered as a visually hidden label. */
  label: string;
  /** Placeholder shown while the field is empty. */
  placeholder: string;
  /** Accessible name of the icon-only clear control. */
  clearLabel: string;
  /** The current, controlled value. */
  value: string;
  /**
   * Called with the new value on every keystroke, and with an empty string when the field is cleared.
   *
   * @param value The new field value.
   */
  onChange(value: string): void;
  /** Optional ref that receives the input element so a parent can move focus to it. */
  inputRef?: RefObject<HTMLInputElement | null>;
}

/**
 * A shared instant-search field.
 *
 * The field is a native search input with a visually hidden label. It filters on every keystroke and has no submit
 * behavior. While the value is not empty an icon-only clear control appears; clearing, by the control or by Escape,
 * reports an empty value and returns focus to the input. The label never wraps the clear control, so the input's
 * accessible name never contains the clear label.
 */
export function SearchField({
  label,
  placeholder,
  clearLabel,
  value,
  onChange,
  inputRef,
}: SearchFieldProps) {
  const inputId = useId();
  const internalRef = useRef<HTMLInputElement | null>(null);
  const ref = inputRef ?? internalRef;

  function clear(): void {
    onChange("");
    ref.current?.focus();
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>): void {
    if (event.key === "Escape" && value !== "") {
      event.preventDefault();
      clear();
    }
  }

  return (
    <div className="search-control">
      <label className="sr-only" htmlFor={inputId}>
        {label}
      </label>
      <Search20Regular className="search-control__icon" aria-hidden="true" />
      <input
        ref={ref}
        id={inputId}
        type="search"
        className="search-control__input"
        value={value}
        placeholder={placeholder}
        autoComplete="off"
        spellCheck={false}
        onChange={(event) => onChange(event.target.value)}
        onKeyDown={handleKeyDown}
      />
      {value !== "" && (
        <ActionButton
          variant="ghost"
          iconOnly
          className="search-control__clear"
          aria-label={clearLabel}
          onClick={clear}
        >
          <Dismiss20Regular aria-hidden="true" />
        </ActionButton>
      )}
    </div>
  );
}
