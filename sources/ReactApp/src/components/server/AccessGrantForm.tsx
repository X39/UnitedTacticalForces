import { useId, type FormEvent, type RefObject } from "react";
import { useTranslation } from "react-i18next";
import type { TranslationKey } from "@/app/localizationTypes";
import { ActionButton } from "@/components/ActionControl";
import { RadioGroupField } from "@/components/FormFields";
import { StatePanel } from "@/components/primitives";
import {
  SearchPicker,
  type PickerOption,
  type SearchPickerStatus,
} from "@/components/SearchPicker";
import { ReasonReserve } from "@/components/server/ReasonReserve";
import { pickerResultLimit } from "@/features/hosting/pickerLimits";
import "@/components/server/access.css";

/** Which kind of subject the grant form gives access to. */
export type AccessGrantKind = "user" | "role";

/** The locale keys that differ between the member and the role flavor of the form. */
interface GrantFlavor {
  /** Label of the picker. */
  label: TranslationKey;
  /** Placeholder of the picker input. */
  placeholder: TranslationKey;
  /** Text of the picker when a settled search finds nothing. */
  empty: TranslationKey;
  /** Reason shown on the Give access button while nothing is chosen. */
  choose: TranslationKey;
}

const flavors: ReadonlyMap<AccessGrantKind, GrantFlavor> = new Map<AccessGrantKind, GrantFlavor>([
  [
    "user",
    {
      label: "Server_Access_Search_Member",
      placeholder: "Server_Access_Search_MemberPlaceholder",
      empty: "Server_Access_Search_MemberEmpty",
      choose: "Server_Access_Grant_ChooseMember",
    },
  ],
  [
    "role",
    {
      label: "Server_Access_Search_Role",
      placeholder: "Server_Access_Search_RolePlaceholder",
      empty: "Server_Access_Search_RoleEmpty",
      choose: "Server_Access_Grant_ChooseRole",
    },
  ],
]);

/** Props of {@link AccessGrantForm}. */
export interface AccessGrantFormProps {
  /** Whether the form searches members or roles. */
  kind: AccessGrantKind;
  /**
   * Called when the member picks the other flavor. The owner clears the query and the choice.
   *
   * @param kind The chosen flavor.
   */
  onKindChange(kind: AccessGrantKind): void;
  /** The current, controlled text of the picker input. */
  query: string;
  /**
   * Called on every change of the picker text.
   *
   * @param value The new text.
   */
  onQueryChange(value: string): void;
  /** The state of the candidate search, derived from the immediate query text. */
  status: SearchPickerStatus;
  /** The current candidates; the previous ones stay while the next search loads. */
  options: readonly PickerOption[];
  /** True when the search found more candidates than `options` holds, so the list tells the member to narrow it. */
  hasMore?: boolean | undefined;
  /** The chosen candidate, or null while none is chosen. */
  selected: PickerOption | null;
  /**
   * Called when the member chooses a candidate.
   *
   * @param option The chosen candidate.
   */
  onSelect(option: PickerOption): void;
  /** Called when the member clears the chosen candidate. */
  onClear(): void;
  /** Called when the member retries a failed search. */
  onRetry(): void;
  /** Whether the grant call is running. The form is then busy and its inputs are disabled. */
  pending: boolean;
  /** Whether the signed-in member may assign View. Without it the button stays unavailable with a reason. */
  canGiveView: boolean;
  /** The message of the last failed grant, or undefined. */
  errorText?: string | undefined;
  /** Called when the member submits a complete choice. It is not called while a grant is pending. */
  onSubmit(): void;
  /** Receives the picker input so the owner can return focus to it after a grant. */
  inputRef?: RefObject<HTMLInputElement | null> | undefined;
}

/**
 * The form that gives a member or a role access to a server: a segmented Member and Role choice, the shared search
 * picker and the Give access button.
 *
 * The form is presentational. The owner holds the query, the choice and the candidates and passes the search state
 * in. The button stays focusable but unavailable, with a visible reason, until a candidate is chosen, and it stays
 * unavailable when the signed-in member may not assign View. While a grant is pending the segmented choice and the
 * picker are disabled, the form is `aria-busy` and the button reads as pending. A failed grant shows its message in
 * an alert under the row and leaves the choice in place so the member can retry.
 */
export function AccessGrantForm({
  kind,
  onKindChange,
  query,
  onQueryChange,
  status,
  options,
  hasMore,
  selected,
  onSelect,
  onClear,
  onRetry,
  pending,
  canGiveView,
  errorText,
  onSubmit,
  inputRef,
}: AccessGrantFormProps) {
  const { t } = useTranslation();
  const baseId = useId();
  const titleId = `${baseId}-title`;
  const flavor = flavors.get(kind);
  if (flavor === undefined) throw new RangeError(`Unknown access subject kind: ${kind}`);

  let unavailableReason: string | undefined;
  if (!canGiveView) unavailableReason = t("Server_Access_Grant_CannotGiveView");
  else if (selected === null) unavailableReason = t(flavor.choose);

  function handleSubmit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    if (pending || selected === null || !canGiveView) return;
    onSubmit();
  }

  return (
    <form
      className="access-grant"
      aria-labelledby={titleId}
      aria-busy={pending}
      noValidate
      onSubmit={handleSubmit}
    >
      <h3 id={titleId} className="access-grant__title">
        {t("Server_Access_Grant_Title")}
      </h3>
      <p className="access-grant__body">{t("Server_Access_Grant_Body")}</p>
      <RadioGroupField
        layout="segmented"
        hideLegend
        legend={t("Server_Access_Subject_Legend")}
        name={`${baseId}-kind`}
        value={kind}
        disabled={pending}
        options={[
          { value: "user", label: t("Server_Access_Subject_Member") },
          { value: "role", label: t("Server_Access_Subject_Role") },
        ]}
        onChange={(value) => {
          if (value === "user" || value === "role") onKindChange(value);
        }}
      />
      <div className="access-grant__row">
        <div className="access-grant__picker">
          <SearchPicker
            label={t(flavor.label)}
            placeholder={t(flavor.placeholder)}
            query={query}
            onQueryChange={onQueryChange}
            options={options}
            hasMore={hasMore}
            status={status}
            selected={selected}
            onSelect={onSelect}
            onClear={onClear}
            onRetry={onRetry}
            disabled={pending}
            inputRef={inputRef}
            texts={{
              loading: t("Picker_Loading"),
              error: t("Picker_Error"),
              retry: t("Picker_Retry"),
              noMatches: t(flavor.empty),
              hintIdle: t("Server_Access_Search_Hint"),
              results: (count) => t("Picker_Results", { count }),
              clear: t("Picker_Clear"),
              refine: t("Picker_Refine", { count: pickerResultLimit }),
            }}
          />
        </div>
        <div className="access-grant__action">
          <ActionButton
            variant="primary"
            type="submit"
            aria-busy={pending ? true : undefined}
            unavailableReason={unavailableReason}
          >
            {pending ? t("Server_Access_Grant_Submitting") : t("Server_Access_Grant_Submit")}
          </ActionButton>
        </div>
        <ReasonReserve
          reasons={[
            t("Server_Access_Grant_CannotGiveView"),
            t("Server_Access_Grant_ChooseMember"),
            t("Server_Access_Grant_ChooseRole"),
          ]}
        />
      </div>
      {errorText !== undefined && <StatePanel kind="error">{errorText}</StatePanel>}
    </form>
  );
}
