import { Checkmark16Filled, ChevronDown20Regular, Warning20Regular } from "@fluentui/react-icons";
import { useId, type ComponentPropsWithRef, type ReactNode } from "react";
import "@/components/server/form-fields.css";

/** Shared chrome props of the labelled form controls: the label, an optional hint, an error and requiredness. */
export interface FieldChromeProps {
  /** Visible label of the control. Always rendered; it is the accessible name of the native control. */
  label: ReactNode;
  /** Optional help text rendered under the label and linked to the control through `aria-describedby`. */
  hint?: ReactNode | undefined;
  /** Validation message. When present the control is marked invalid and the message is announced as an alert. */
  error?: string | undefined;
  /**
   * Sets the native `required` attribute, exposes `aria-required` unless the caller passes its own value, and shows a
   * decorative asterisk that assistive technology skips.
   */
  required?: boolean | undefined;
}

/** Ids and the merged `aria-describedby` value that tie a control to its hint and error. */
interface FieldWiring {
  /** Id of the native control. A caller supplied id wins over the generated one. */
  controlId: string;
  /** Id of the hint element. */
  hintId: string;
  /** Id of the error element. */
  errorId: string;
  /** The space separated ids, or `undefined` when nothing describes the control. */
  describedBy: string | undefined;
}

/**
 * Derives the control, hint and error ids and merges them into one `aria-describedby` value.
 *
 * @param providedId An id passed by the caller, if any.
 * @param providedDescribedBy An `aria-describedby` value passed by the caller, if any.
 * @param hasHint Whether a hint is rendered.
 * @param hasError Whether an error is rendered.
 * @returns The wiring for the control, with the hint id first, then the error id, then the caller ids.
 */
function useFieldWiring(
  providedId: string | undefined,
  providedDescribedBy: string | undefined,
  hasHint: boolean,
  hasError: boolean,
): FieldWiring {
  const generatedId = useId();
  const controlId = providedId ?? generatedId;
  const hintId = `${controlId}-hint`;
  const errorId = `${controlId}-error`;
  const ids: string[] = [];
  if (hasHint) ids.push(hintId);
  if (hasError) ids.push(errorId);
  if (providedDescribedBy) ids.push(providedDescribedBy);
  return { controlId, hintId, errorId, describedBy: ids.length > 0 ? ids.join(" ") : undefined };
}

/** Props of the internal {@link FieldLabel}. */
interface FieldLabelProps {
  /** Id of the control the label names. */
  controlId: string;
  /** The label content. */
  label: ReactNode;
  /** Whether to show the decorative required marker. */
  required: boolean | undefined;
}

/**
 * Renders the visible label and the decorative required marker of a control.
 *
 * @param props The label props.
 * @returns The label element.
 */
function FieldLabel({ controlId, label, required }: FieldLabelProps) {
  return (
    <label className="form-field__label" htmlFor={controlId}>
      {label}
      {required && (
        <span className="form-field__required" aria-hidden="true">
          {" *"}
        </span>
      )}
    </label>
  );
}

/** Props of the internal {@link FieldHead}. */
interface FieldHeadProps {
  /** Content at the end of the label row. When absent the label renders without a wrapper. */
  labelEnd: ReactNode;
  /** The label element. */
  children: ReactNode;
}

/**
 * Renders the label and, only when `labelEnd` is given, the end node in one wrapping row.
 *
 * @param props The head props.
 * @returns The label alone, or the label and the node inside `form-field__head`.
 */
function FieldHead({ labelEnd, children }: FieldHeadProps) {
  if (labelEnd == null) return <>{children}</>;
  return (
    <div className="form-field__head">
      {children}
      {labelEnd}
    </div>
  );
}

/** Props of the internal {@link FieldHint}. */
interface FieldHintProps {
  /** Id the control references from `aria-describedby`. */
  id: string;
  /** The hint content. */
  children: ReactNode;
}

/**
 * Renders the hint paragraph of a control.
 *
 * @param props The hint props.
 * @returns The hint element.
 */
function FieldHint({ id, children }: FieldHintProps) {
  return (
    <p className="form-field__hint" id={id}>
      {children}
    </p>
  );
}

/** Props of {@link FieldError}. */
export interface FieldErrorProps {
  /** Id the control references from `aria-describedby`. */
  id: string;
  /** The validation message. */
  message: string;
}

/**
 * Renders the validation message of a control as an announced alert with an inline warning icon.
 *
 * @param props The error props.
 * @returns The error element.
 */
export function FieldError({ id, message }: FieldErrorProps) {
  return (
    <p className="form-field__error" id={id} role="alert">
      <Warning20Regular className="form-field__error-icon" aria-hidden="true" />
      <span>{message}</span>
    </p>
  );
}

/** Props of {@link TextField}: the chrome props, an optional trailing node and every native input prop except the styling hooks. */
export type TextFieldProps = FieldChromeProps &
  Omit<ComponentPropsWithRef<"input">, "className" | "children" | "required"> & {
    /**
     * Content rendered to the right of the control in one row with it, for example a Remove button. The label and
     * the hint stay above the row and the error stays below it, so the node always lines up with the control and
     * never with the error line.
     */
    trailing?: ReactNode | undefined;
    /**
     * Content at the end of the label row, for example a field's change state; without it the markup is unchanged.
     */
    labelEnd?: ReactNode | undefined;
  };

/**
 * A labelled native text input.
 *
 * The label is always visible and bound to the input. The hint and the error are linked through
 * `aria-describedby`; `aria-invalid` is only set while an error exists and the error is announced with
 * `role="alert"`. Every other prop, including `name`, `onChange`, `onBlur` and `ref`, reaches the native input, so the
 * spread result of react-hook-form's `register` works and `setFocus` can focus the control. The optional `trailing`
 * node shares one row with the control; without it the markup is unchanged.
 */
export function TextField({
  label,
  hint,
  error,
  required,
  id,
  trailing,
  labelEnd,
  "aria-describedby": describedByProp,
  "aria-required": ariaRequiredProp,
  ...inputProps
}: TextFieldProps) {
  const wiring = useFieldWiring(id, describedByProp, hint != null, error != null);
  const control = (
    <input
      {...inputProps}
      id={wiring.controlId}
      className="form-field__control"
      required={required}
      aria-required={ariaRequiredProp ?? (required ? true : undefined)}
      aria-invalid={error != null ? true : undefined}
      aria-describedby={wiring.describedBy}
    />
  );
  return (
    <div className="form-field">
      <FieldHead labelEnd={labelEnd}>
        <FieldLabel controlId={wiring.controlId} label={label} required={required} />
      </FieldHead>
      {hint != null && <FieldHint id={wiring.hintId}>{hint}</FieldHint>}
      {trailing != null ? (
        <div className="form-field__row">
          {control}
          <div className="form-field__trailing">{trailing}</div>
        </div>
      ) : (
        control
      )}
      {error != null && <FieldError id={wiring.errorId} message={error} />}
    </div>
  );
}

/** Props of {@link TextAreaField}: the chrome props plus every native textarea prop except the styling hooks. */
export type TextAreaFieldProps = FieldChromeProps &
  Omit<ComponentPropsWithRef<"textarea">, "className" | "children" | "required"> & {
    /**
     * Content at the end of the label row, for example a field's change state; without it the markup is unchanged.
     */
    labelEnd?: ReactNode | undefined;
  };

/**
 * A labelled native multi-line text input, the sibling of {@link TextField}.
 *
 * It follows the same label, hint, error and `aria-describedby` rules and the same minimum control height rules as
 * `TextField`; the control is at least two rows tall and can be resized vertically. Every other prop, including
 * `rows`, `value`, `onChange` and `ref`, reaches the native textarea.
 */
export function TextAreaField({
  label,
  hint,
  error,
  required,
  id,
  labelEnd,
  "aria-describedby": describedByProp,
  "aria-required": ariaRequiredProp,
  ...textAreaProps
}: TextAreaFieldProps) {
  const wiring = useFieldWiring(id, describedByProp, hint != null, error != null);
  return (
    <div className="form-field">
      <FieldHead labelEnd={labelEnd}>
        <FieldLabel controlId={wiring.controlId} label={label} required={required} />
      </FieldHead>
      {hint != null && <FieldHint id={wiring.hintId}>{hint}</FieldHint>}
      <textarea
        {...textAreaProps}
        id={wiring.controlId}
        className="form-field__control form-field__control--multiline"
        required={required}
        aria-required={ariaRequiredProp ?? (required ? true : undefined)}
        aria-invalid={error != null ? true : undefined}
        aria-describedby={wiring.describedBy}
      />
      {error != null && <FieldError id={wiring.errorId} message={error} />}
    </div>
  );
}

/** One selectable entry of a {@link SelectField}. */
export interface SelectOption {
  /** The submitted value. */
  value: string;
  /** The visible label. */
  label: string;
  /** Whether the option is shown but cannot be chosen. */
  disabled?: boolean | undefined;
  /** BCP 47 language of the label when it differs from the page language. */
  lang?: string | undefined;
}

/** A labelled group of {@link SelectOption}s rendered as a native `optgroup`. */
export interface SelectOptionGroup {
  /** The visible group label. */
  group: string;
  /** The options inside the group. */
  options: readonly SelectOption[];
}

/** Props of {@link SelectField}: the chrome props plus every native select prop and the option list. */
export type SelectFieldProps = FieldChromeProps &
  Omit<ComponentPropsWithRef<"select">, "className" | "children" | "required"> & {
    /** Plain options and option groups, in display order. */
    options: readonly (SelectOption | SelectOptionGroup)[];
    /**
     * Content at the end of the label row, for example a field's change state; without it the markup is unchanged.
     */
    labelEnd?: ReactNode | undefined;
  };

/**
 * Renders one native option.
 *
 * @param option The option to render.
 * @returns The option element.
 */
function renderOption(option: SelectOption) {
  return (
    <option key={option.value} value={option.value} disabled={option.disabled} lang={option.lang}>
      {option.label}
    </option>
  );
}

/**
 * A labelled native select with optional option groups and the shared padded chevron.
 *
 * Long lists stay inside the native popup, so they never grow the page. The browser arrow is removed and replaced by
 * one decorative, `aria-hidden` chevron that sits in the control's end padding, so the text never runs under it. Label,
 * hint, error and prop pass-through behave exactly as in {@link TextField}.
 */
export function SelectField({
  label,
  hint,
  error,
  required,
  options,
  id,
  labelEnd,
  "aria-describedby": describedByProp,
  "aria-required": ariaRequiredProp,
  ...selectProps
}: SelectFieldProps) {
  const wiring = useFieldWiring(id, describedByProp, hint != null, error != null);
  return (
    <div className="form-field">
      <FieldHead labelEnd={labelEnd}>
        <FieldLabel controlId={wiring.controlId} label={label} required={required} />
      </FieldHead>
      {hint != null && <FieldHint id={wiring.hintId}>{hint}</FieldHint>}
      <div className="form-field__select">
        <select
          {...selectProps}
          id={wiring.controlId}
          className="form-field__control form-field__control--select"
          required={required}
          aria-required={ariaRequiredProp ?? (required ? true : undefined)}
          aria-invalid={error != null ? true : undefined}
          aria-describedby={wiring.describedBy}
        >
          {options.map((item) =>
            "group" in item ? (
              <optgroup key={item.group} label={item.group}>
                {item.options.map(renderOption)}
              </optgroup>
            ) : (
              renderOption(item)
            ),
          )}
        </select>
        <ChevronDown20Regular className="form-field__select-chevron" aria-hidden="true" />
      </div>
      {error != null && <FieldError id={wiring.errorId} message={error} />}
    </div>
  );
}

/** How a {@link CheckboxField} is drawn. */
export type CheckboxFieldVariant = "box" | "chip";

/** Props of {@link CheckboxField}: the chrome props without `required` plus every native input prop except `type`. */
export type CheckboxFieldProps = Omit<FieldChromeProps, "required"> &
  Omit<ComponentPropsWithRef<"input">, "className" | "children" | "type"> & {
    /**
     * `box` (the default) draws a check box with a check mark. `chip` draws a toggle chip with no visible box: the
     * chip inverts its fill when checked, which suits short labels such as weekday names.
     */
    variant?: CheckboxFieldVariant | undefined;
  };

/**
 * A labelled native checkbox drawn by the project.
 *
 * The label wraps a real native checkbox that stays in the accessibility tree, is reached by Tab and is operated by
 * Space. The input is invisible and overlays the whole row, so the whole row is the click target; the drawn box is an
 * `aria-hidden` sibling of the input, never its ancestor. The `chip` variant draws the same native checkbox as a chip
 * instead of a box. The hint and the error are linked through `aria-describedby`. Every other prop, including
 * `name`, `onChange` and `ref`, reaches the native input, so the spread result of react-hook-form's `register`
 * reports the checked state.
 */
export function CheckboxField({
  label,
  hint,
  error,
  id,
  variant,
  "aria-describedby": describedByProp,
  ...inputProps
}: CheckboxFieldProps) {
  const wiring = useFieldWiring(id, describedByProp, hint != null, error != null);
  const input = (
    <input
      {...inputProps}
      id={wiring.controlId}
      type="checkbox"
      className="check-mark__input"
      aria-invalid={error != null ? true : undefined}
      aria-describedby={wiring.describedBy}
    />
  );
  return (
    <div className="form-field form-field--check">
      {variant === "chip" ? (
        <label className="toggle-chip" htmlFor={wiring.controlId}>
          {input}
          <span className="toggle-chip__text">{label}</span>
        </label>
      ) : (
        <label className="form-field__check" htmlFor={wiring.controlId}>
          <span className="check-mark">
            {input}
            <span className="check-mark__box" aria-hidden="true">
              <Checkmark16Filled />
            </span>
          </span>
          <span className="form-field__check-text">{label}</span>
        </label>
      )}
      {hint != null && <FieldHint id={wiring.hintId}>{hint}</FieldHint>}
      {error != null && <FieldError id={wiring.errorId} message={error} />}
    </div>
  );
}

/** Props of {@link SwitchField}: the chrome props without `required` plus every native input prop except the type and role. */
export type SwitchFieldProps = Omit<FieldChromeProps, "required"> &
  Omit<ComponentPropsWithRef<"input">, "className" | "children" | "type" | "role"> & {
    /** The state word shown next to the label while the switch is on. Decorative: the accessible name is the label. */
    onLabel: string;
    /** The state word shown next to the label while the switch is off. Decorative: the accessible name is the label. */
    offLabel: string;
    /**
     * Content at the end of the label row, for example a field's change state; without it the markup is unchanged.
     */
    labelEnd?: ReactNode | undefined;
  };

/**
 * A labelled switch: a native checkbox with `role="switch"`, drawn as a track with a thumb.
 *
 * The native input keeps its place in the accessibility tree, is operated by Space and reports its state through the
 * browser's checked state, so no `aria-checked` is authored. The drawn track and both state words are `aria-hidden`
 * and siblings of the input; CSS shows the word that matches the checked state, so the state never depends on color
 * alone. A disabled switch stays visible with its state. Every other prop, including `name`, `onChange` and `ref`,
 * reaches the native input.
 */
export function SwitchField({
  label,
  hint,
  error,
  id,
  onLabel,
  offLabel,
  labelEnd,
  "aria-describedby": describedByProp,
  ...inputProps
}: SwitchFieldProps) {
  const wiring = useFieldWiring(id, describedByProp, hint != null, error != null);
  const row = (
    <label className="form-field__switch" htmlFor={wiring.controlId}>
      <span className="form-field__switch-text">{label}</span>
      <span className="switch-control">
        <input
          {...inputProps}
          id={wiring.controlId}
          type="checkbox"
          role="switch"
          className="check-mark__input"
          aria-invalid={error != null ? true : undefined}
          aria-describedby={wiring.describedBy}
        />
        <span className="switch-track" aria-hidden="true">
          <span className="switch-thumb" />
        </span>
      </span>
      <span className="form-field__switch-state form-field__switch-state--on" aria-hidden="true">
        {onLabel}
      </span>
      <span className="form-field__switch-state form-field__switch-state--off" aria-hidden="true">
        {offLabel}
      </span>
    </label>
  );
  return (
    <div className="form-field form-field--switch">
      {labelEnd != null ? (
        <div className="form-field__head">
          {row}
          {labelEnd}
        </div>
      ) : (
        row
      )}
      {hint != null && <FieldHint id={wiring.hintId}>{hint}</FieldHint>}
      {error != null && <FieldError id={wiring.errorId} message={error} />}
    </div>
  );
}

/** One choice of a {@link RadioGroupField}. */
export interface RadioOption {
  /** The value reported through `onChange` when the option is chosen. */
  value: string;
  /** The visible label of the option. */
  label: ReactNode;
  /** Optional help text under the label, linked to the radio through `aria-describedby`. Shown by cards only. */
  help?: ReactNode | undefined;
  /** Whether the option stays visible but cannot be chosen. */
  disabled?: boolean | undefined;
}

/** How a {@link RadioGroupField} draws its options. */
export type RadioGroupFieldLayout = "cards" | "segmented";

/** Props of {@link RadioGroupField}. */
export interface RadioGroupFieldProps {
  /** Group caption. It may hold a heading element, which a `legend` allows. */
  legend: ReactNode;
  /** The shared `name` of the radios. */
  name: string;
  /** The value of the chosen option. */
  value: string;
  /** The choices in display order. */
  options: readonly RadioOption[];
  /**
   * Called with the value of the option the user chose.
   *
   * @param value The chosen option value.
   */
  onChange(value: string): void;
  /** Visible text that explains why some options are disabled. Linked to every disabled radio. */
  reason?: ReactNode | undefined;
  /** Disables every radio of the group. */
  disabled?: boolean | undefined;
  /**
   * `cards` (the default) draws one choice card per option, with its help text. `segmented` draws one bordered bar
   * with one segment per option and no help text, which suits a few short choices.
   */
  layout?: RadioGroupFieldLayout | undefined;
  /** Keeps the legend for assistive technology only: it stays in the accessibility tree but is not drawn. */
  hideLegend?: boolean | undefined;
}

/**
 * A labelled group of native radio buttons inside a fieldset, drawn as choice cards or as a segmented bar.
 *
 * The native radios stay in the accessibility tree and keep the native keyboard behavior: Tab enters the group at the
 * selected radio, the Arrow keys move and select, Space selects. Disabled options stay visible. When a `reason` is
 * given it renders once below the options and every disabled radio references it through `aria-describedby`, so the
 * explanation is available to assistive technology as well. A group of two or three cards lays out in equal columns
 * above 520px.
 */
export function RadioGroupField({
  legend,
  name,
  value,
  options,
  onChange,
  reason,
  disabled,
  layout,
  hideLegend,
}: RadioGroupFieldProps) {
  const baseId = useId();
  const reasonId = `${baseId}-reason`;
  const segmented = layout === "segmented";
  const rows = options.map((option, index) => {
    const isDisabled = disabled === true || option.disabled === true;
    const helpId = `${baseId}-help-${index}`;
    const ids: string[] = [];
    if (!segmented && option.help != null) ids.push(helpId);
    if (isDisabled && reason != null) ids.push(reasonId);
    const radio = (
      <input
        type="radio"
        name={name}
        value={option.value}
        checked={option.value === value}
        disabled={isDisabled}
        className="check-mark__input"
        aria-describedby={ids.length > 0 ? ids.join(" ") : undefined}
        onChange={() => onChange(option.value)}
      />
    );
    if (segmented) {
      return (
        <label
          key={option.value}
          className={
            isDisabled ? "segmented__option segmented__option--disabled" : "segmented__option"
          }
        >
          {radio}
          <span className="segmented__text">{option.label}</span>
        </label>
      );
    }
    return (
      <label
        key={option.value}
        className={
          isDisabled
            ? "choice-card form-field__radio-row form-field__radio-row--disabled"
            : "choice-card form-field__radio-row"
        }
      >
        <span className="check-mark check-mark--radio">
          {radio}
          <span className="check-mark__box" aria-hidden="true" />
        </span>
        <span className="form-field__radio-text">
          <span className="choice-card__label">{option.label}</span>
          {option.help != null && <FieldHint id={helpId}>{option.help}</FieldHint>}
        </span>
      </label>
    );
  });
  const columns = options.length === 2 || options.length === 3 ? options.length : undefined;
  return (
    <fieldset
      className="form-field form-field--radio"
      data-layout={segmented ? "segmented" : "cards"}
    >
      <legend className={hideLegend === true ? "form-field__legend sr-only" : "form-field__legend"}>
        {legend}
      </legend>
      {segmented ? (
        <div className="segmented">{rows}</div>
      ) : (
        <div className="form-field__options" data-columns={columns}>
          {rows}
        </div>
      )}
      {reason != null && (
        <p className="form-field__reason" id={reasonId}>
          {reason}
        </p>
      )}
    </fieldset>
  );
}
