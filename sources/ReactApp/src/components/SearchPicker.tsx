import { Spinner } from "@fluentui/react-components";
import { Dismiss16Regular } from "@fluentui/react-icons";
import {
  useEffect,
  useId,
  useRef,
  useState,
  type KeyboardEvent,
  type MouseEvent,
  type PointerEvent,
  type ReactNode,
  type RefObject,
} from "react";
import { ActionButton } from "@/components/ActionControl";
import { TextField } from "@/components/FormFields";
import "@/components/search-picker.css";

/** One choice of a {@link SearchPicker}: a member, a role or any other searchable subject. */
export interface PickerOption {
  /** Stable identifier of the option. It is unique inside one option list and part of the generated element id. */
  id: string;
  /** The name of the option, rendered in an isolated bidirectional run. */
  label: string;
  /** Secondary text of the same item, for example an email address, so equal names stay distinguishable. */
  detail?: string | undefined;
  /** Decorative icon shown before the name. It is hidden from assistive technology. */
  icon?: ReactNode | undefined;
  /** Marks the option as not selectable. Arrow keys skip it and activation ignores it. */
  disabled?: boolean | undefined;
  /** Visible explanation shown inside a disabled option, for example why the subject cannot be chosen. */
  disabledReason?: string | undefined;
}

/** The localized strings of a {@link SearchPicker}. The component contains no literal user-facing text. */
export interface SearchPickerTexts {
  /** Text of the loading row. */
  loading: string;
  /** Text of the error row. */
  error: string;
  /** Label of the retry control inside the error row. */
  retry: string;
  /** Text of the row and of the live announcement shown when a settled query has no match. */
  noMatches: string;
  /** Hint shown under the input while the minimum query length is not met. Omit it to show no hint. */
  hintIdle?: string | undefined;
  /**
   * Builds the live announcement for a settled query. A function instead of a singular and a plural string lets the
   * caller's localization library select the plural form.
   *
   * @param count The number of options of the settled query.
   * @returns The text announced politely, for example "3 results".
   */
  results(count: number): string;
  /** Accessible name of the icon-only control that clears the chosen option. */
  clear: string;
  /**
   * Text of the muted row, and part of the live announcement, shown after the options when the caller reports that more
   * matches exist than it passes. Omit it to show no such row.
   */
  refine?: string | undefined;
}

/** The lifecycle of the query that produced the options. */
export type SearchPickerStatus = "idle" | "loading" | "error" | "ready";

/** Props of {@link SearchPicker}. */
export interface SearchPickerProps {
  /** Visible label above the input. It is also the accessible name of the listbox. */
  label: string;
  /** Placeholder shown while the input is empty. */
  placeholder: string;
  /** The current, controlled input text. */
  query: string;
  /**
   * Called on every change of the input text and with an empty string when a second Escape clears it.
   *
   * @param value The new input text.
   */
  onQueryChange(value: string): void;
  /** The current matches. While a new query loads the caller keeps passing the previous matches. */
  options: readonly PickerOption[];
  /** True when the query has more matches than `options` holds, so the list ends with the `texts.refine` row. */
  hasMore?: boolean | undefined;
  /**
   * The state of the query. `idle` means the minimum query length is not met; derive it from the immediate text, not
   * from a debounced copy, so the listbox does not wait for the debounce. `loading` keeps showing the options.
   */
  status: SearchPickerStatus;
  /** The chosen option. While it is set the input is replaced by a token of the same height. */
  selected: PickerOption | null;
  /**
   * Called when the user chooses an enabled option.
   *
   * @param option The chosen option.
   */
  onSelect(option: PickerOption): void;
  /** Called when the user clears the chosen option. Focus then returns to the re-mounted input. */
  onClear(): void;
  /** Minimum number of characters before the listbox opens. `0` lists options as soon as the input has focus. */
  minQueryLength?: number | undefined;
  /** The localized strings of the listbox states, the live announcement and the clear control. */
  texts: SearchPickerTexts;
  /** Called by the retry control of the error row and by Enter while the error row shows. */
  onRetry(): void;
  /** Disables the input and the clear control and keeps the listbox closed. */
  disabled?: boolean | undefined;
  /** Id of an external element that also describes the input, for example a form-level hint. */
  describedBy?: string | undefined;
  /** Called when the input gains focus, so a caller can start a lazy query. */
  onFocus?: (() => void) | undefined;
  /** Optional ref that receives the input element so a parent can move focus to it. */
  inputRef?: RefObject<HTMLInputElement | null> | undefined;
}

/** A pending request to move focus, identified by a serial so repeated requests are distinct state values. */
interface FocusRequest {
  /** The element that receives focus. */
  target: "input" | "clear";
  /** Increases with every request. */
  serial: number;
}

/**
 * Builds the element id of one option.
 *
 * @param listboxId The id of the listbox.
 * @param optionId The id of the option.
 * @returns The stable element id used by `aria-activedescendant`.
 */
function optionElementId(listboxId: string, optionId: string): string {
  return `${listboxId}-option-${optionId}`;
}

/**
 * A searchable single choice: an ARIA 1.2 combobox with a list autocomplete and an inline listbox.
 *
 * The input is a shared `TextField` and keeps focus at all times; the listbox is never focusable and the active option
 * is exposed through `aria-activedescendant`. The listbox opens when the input has at least `minQueryLength`
 * characters (typing, focus, or ArrowDown) and closes on Escape, Tab, focus leaving the picker and selection. While the
 * error row shows, a forward Tab moves to its Retry control instead of closing. ArrowDown and ArrowUp wrap,
 * Home and End jump while the listbox is open, Enter chooses the active option without submitting a surrounding form,
 * and disabled options are skipped. A mouse chooses an option on pointer down with the default prevented, so the input
 * keeps focus; touch and pen choose on the click of a tap, so a scroll gesture inside the listbox never chooses. A chosen option replaces the input by a token of the same height with an icon-only clear control. The loading,
 * error and no-match states are rows in the listbox slot, the idle hint sits under the input, and a visually hidden
 * polite live region announces the result count of each settled query.
 */
export function SearchPicker({
  label,
  placeholder,
  query,
  onQueryChange,
  options,
  hasMore = false,
  status,
  selected,
  onSelect,
  onClear,
  minQueryLength = 1,
  texts,
  onRetry,
  disabled = false,
  describedBy,
  onFocus,
  inputRef,
}: SearchPickerProps) {
  const baseId = useId();
  const listboxId = `${baseId}-listbox`;
  const hintId = `${baseId}-hint`;
  const labelId = `${baseId}-label`;
  const internalRef = useRef<HTMLInputElement | null>(null);
  const ref = inputRef ?? internalRef;
  const clearRef = useRef<HTMLButtonElement | null>(null);
  const rootRef = useRef<HTMLDivElement | null>(null);
  const lastPointer = useRef<{ x: number; y: number } | null>(null);
  const [open, setOpen] = useState(false);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [focusRequest, setFocusRequest] = useState<FocusRequest | null>(null);

  const enabledOptions = options.filter((option) => option.disabled !== true);
  const expanded =
    open &&
    !disabled &&
    selected === null &&
    status !== "idle" &&
    query.trim().length >= minQueryLength;
  const activeOption = expanded
    ? enabledOptions.find((option) => option.id === activeId)
    : undefined;
  const activeElementId =
    activeOption === undefined ? undefined : optionElementId(listboxId, activeOption.id);

  useEffect(() => {
    if (focusRequest === null) return;
    const element = focusRequest.target === "input" ? ref.current : clearRef.current;
    element?.focus();
  }, [focusRequest, ref]);

  useEffect(() => {
    if (activeElementId === undefined) return;
    document.getElementById(activeElementId)?.scrollIntoView?.({ block: "nearest" });
  }, [activeElementId]);

  function requestFocus(target: FocusRequest["target"]): void {
    setFocusRequest((previous) => ({ target, serial: (previous?.serial ?? 0) + 1 }));
  }

  function choose(option: PickerOption): void {
    if (option.disabled === true) return;
    onSelect(option);
    setOpen(false);
    setActiveId(null);
    requestFocus("clear");
  }

  function clear(): void {
    onClear();
    requestFocus("input");
  }

  function close(): void {
    setOpen(false);
    setActiveId(null);
  }

  function moveActive(forward: boolean): void {
    const count = enabledOptions.length;
    if (count === 0) return;
    const index = enabledOptions.findIndex((option) => option.id === activeOption?.id);
    const next = forward ? (index + 1) % count : index <= 0 ? count - 1 : index - 1;
    setActiveId(enabledOptions[next]?.id ?? null);
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>): void {
    switch (event.key) {
      case "ArrowDown":
      case "ArrowUp": {
        event.preventDefault();
        const forward = event.key === "ArrowDown";
        if (expanded) {
          moveActive(forward);
        } else {
          setOpen(true);
          setActiveId((forward ? enabledOptions[0] : enabledOptions.at(-1))?.id ?? null);
        }
        break;
      }
      case "Home":
      case "End":
        if (expanded && enabledOptions.length > 0) {
          event.preventDefault();
          setActiveId(
            (event.key === "Home" ? enabledOptions[0] : enabledOptions.at(-1))?.id ?? null,
          );
        }
        break;
      case "Enter":
        // Enter that confirms an IME composition belongs to the composition, not to the listbox.
        if (event.nativeEvent.isComposing) break;
        if (expanded && activeOption !== undefined) {
          event.preventDefault();
          choose(activeOption);
        } else if (expanded && status === "error") {
          event.preventDefault();
          onRetry();
        }
        break;
      case "Escape":
        if (event.nativeEvent.isComposing) break;
        if (expanded) {
          event.preventDefault();
          close();
        } else if (query !== "") {
          event.preventDefault();
          onQueryChange("");
        }
        break;
      case "Tab":
        // While the error row shows, a forward Tab walks into its Retry control; every other Tab leaves the picker.
        if (!(expanded && status === "error") || event.shiftKey) close();
        break;
      default:
        break;
    }
  }

  function handleOptionPointerDown(event: PointerEvent<HTMLLIElement>, option: PickerOption): void {
    if (event.button !== 0) return;
    // A touch or pen contact may start a scroll gesture inside the listbox, so it must not choose at contact time;
    // the browser fires `click` only for a tap and suppresses it after a scroll.
    if (event.pointerType === "touch" || event.pointerType === "pen") return;
    event.preventDefault();
    choose(option);
  }

  function handleOptionPointerMove(event: PointerEvent<HTMLLIElement>, option: PickerOption): void {
    // Only a real mouse movement may take the active option from the keyboard. Chromium also dispatches a move under a
    // resting pointer when the listbox scrolls, and that one carries the same coordinates.
    if (event.pointerType === "touch" || event.pointerType === "pen" || option.disabled === true)
      return;
    const last = lastPointer.current;
    if (last !== null && last.x === event.clientX && last.y === event.clientY) return;
    lastPointer.current = { x: event.clientX, y: event.clientY };
    setActiveId(option.id);
  }

  /**
   * Completes the activation of an option. A mouse already chose on pointer down, which unmounts the listbox before the
   * click arrives; the click only completes the tap of a touch or pen contact and the activation by assistive
   * technology. The listener sits on the listbox and finds the option under the target, because the options are never
   * focusable: the keyboard drives them through the combobox input, so an option needs no key handler of its own.
   */
  function handleListboxClick(event: MouseEvent<HTMLUListElement>): void {
    const element = (event.target as Element).closest<HTMLElement>('[role="option"]');
    const option = options.find((entry) => optionElementId(listboxId, entry.id) === element?.id);
    if (option !== undefined) choose(option);
  }

  const describedByIds = [
    status === "idle" && texts.hintIdle !== undefined ? hintId : undefined,
    describedBy,
  ].filter((id): id is string => id !== undefined);

  const showRefine = status === "ready" && hasMore && texts.refine !== undefined;
  let liveText = "";
  if (expanded && status === "ready") {
    liveText = options.length > 0 ? texts.results(options.length) : texts.noMatches;
    if (showRefine) liveText = `${liveText} ${texts.refine}`;
  }

  return (
    <div
      className="search-picker"
      ref={rootRef}
      onBlur={(event) => {
        // Close only when focus leaves the whole picker, so the Retry control of the error row stays reachable.
        if (!rootRef.current?.contains(event.relatedTarget as Node | null)) close();
      }}
      onKeyDown={(event) => {
        // The input handles its own keys. Escape from the Retry control closes the list like everywhere else.
        if (event.key !== "Escape" || event.target === ref.current || !expanded) return;
        event.preventDefault();
        // Focus first: the input opens the list when it gains focus, and the close must be the last word.
        ref.current?.focus();
        close();
      }}
    >
      {selected === null ? (
        <TextField
          ref={ref}
          type="text"
          role="combobox"
          label={label}
          placeholder={placeholder}
          value={query}
          disabled={disabled}
          autoComplete="off"
          spellCheck={false}
          aria-autocomplete="list"
          aria-expanded={expanded}
          aria-controls={listboxId}
          aria-activedescendant={activeElementId}
          aria-busy={status === "loading" ? true : undefined}
          aria-describedby={describedByIds.length > 0 ? describedByIds.join(" ") : undefined}
          onChange={(event) => {
            onQueryChange(event.target.value);
            setActiveId(null);
            setOpen(true);
          }}
          onKeyDown={handleKeyDown}
          onFocus={() => {
            setOpen(true);
            onFocus?.();
          }}
        />
      ) : (
        <div className="form-field">
          <span className="form-field__label" id={labelId}>
            {label}
          </span>
          <div className="search-picker__chosen" role="group" aria-labelledby={labelId}>
            {selected.icon != null && (
              <span className="search-picker__icon" aria-hidden="true">
                {selected.icon}
              </span>
            )}
            <span className="search-picker__text">
              <bdi className="search-picker__name">{selected.label}</bdi>
              {selected.detail !== undefined && (
                <>
                  {" "}
                  <span className="search-picker__detail">{selected.detail}</span>
                </>
              )}
            </span>
            <ActionButton
              ref={clearRef}
              variant="ghost"
              iconOnly
              className="search-picker__clear"
              aria-label={texts.clear}
              disabled={disabled}
              onClick={clear}
            >
              <Dismiss16Regular aria-hidden="true" />
            </ActionButton>
          </div>
        </div>
      )}
      {status === "idle" && texts.hintIdle !== undefined && selected === null && (
        <p className="search-picker__hint" id={hintId}>
          {texts.hintIdle}
        </p>
      )}
      <div role="status" className="sr-only">
        {liveText}
      </div>
      {expanded && (
        <>
          {/*
            A listbox may only own options. While the search runs the listbox is marked busy, so its loading line is
            allowed; the empty line is hidden from assistive technology because the status region above announces it;
            the error line, which holds an alert and a button, sits in a box of its own next to the listbox.
          */}
          <ul
            id={listboxId}
            role="listbox"
            aria-label={label}
            aria-busy={status === "loading" ? true : undefined}
            className="search-picker__listbox"
            onMouseDown={(event) => event.preventDefault()}
            onClick={handleListboxClick}
          >
            {status === "loading" && (
              <li role="none" className="search-picker__state">
                <span aria-hidden="true">
                  <Spinner size="tiny" />
                </span>
                <span>{texts.loading}</span>
              </li>
            )}
            {status === "ready" && options.length === 0 && (
              <li role="none" aria-hidden="true" className="search-picker__state">
                {texts.noMatches}
              </li>
            )}
            {options.map((option) => {
              const active = option.id === activeOption?.id;
              const optionDisabled = option.disabled === true;
              return (
                <li
                  key={option.id}
                  id={optionElementId(listboxId, option.id)}
                  role="option"
                  aria-selected={active}
                  aria-disabled={optionDisabled ? true : undefined}
                  className={[
                    "search-picker__option",
                    active ? "search-picker__option--active" : undefined,
                    optionDisabled ? "search-picker__option--disabled" : undefined,
                  ]
                    .filter(Boolean)
                    .join(" ")}
                  onPointerDown={(event) => handleOptionPointerDown(event, option)}
                  onPointerMove={(event) => handleOptionPointerMove(event, option)}
                >
                  {option.icon != null && (
                    <span className="search-picker__icon" aria-hidden="true">
                      {option.icon}
                    </span>
                  )}
                  <span className="search-picker__text">
                    <bdi className="search-picker__name">{option.label}</bdi>
                    {option.detail !== undefined && (
                      <>
                        {" "}
                        <span className="search-picker__detail">{option.detail}</span>
                      </>
                    )}
                    {optionDisabled && option.disabledReason !== undefined && (
                      <>
                        {" "}
                        <span className="search-picker__reason">{option.disabledReason}</span>
                      </>
                    )}
                  </span>
                </li>
              );
            })}
            {showRefine && (
              <li role="none" aria-hidden="true" className="search-picker__state">
                {texts.refine}
              </li>
            )}
          </ul>
          {status === "error" && (
            <div
              className="search-picker__listbox search-picker__listbox--state"
              onMouseDown={(event) => event.preventDefault()}
            >
              <div className="search-picker__state">
                <span role="alert">{texts.error}</span>
                <ActionButton
                  variant="ghost"
                  size="compact"
                  onClick={() => {
                    onRetry();
                    // The error row is replaced while the retry loads, so focus returns to the input.
                    ref.current?.focus();
                  }}
                >
                  {texts.retry}
                </ActionButton>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
