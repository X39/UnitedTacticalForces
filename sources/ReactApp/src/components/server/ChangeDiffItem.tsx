import {
  ArrowRight16Regular,
  Checkmark16Regular,
  Dismiss16Regular,
  Warning20Regular,
} from "@fluentui/react-icons";
import type { TFunction } from "i18next";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { ApiText } from "@/components/server/ApiText";
import type { ItemResult, ReviewItem, ReviewValue } from "@/features/hosting/serverChangeDraft";

/** Props of {@link ChangeDiffItem}. */
export interface ChangeDiffItemProps {
  /** The review row to show. */
  item: ReviewItem;
  /** The outcome of the last apply run for the step this row belongs to, if there was one. */
  state?: ItemResult | undefined;
  /** Removes the row from the staged changes; the button is omitted when absent. */
  onRemove?: (() => void) | undefined;
  /** Extra content below the values, for example the contents diff of a mod pack. */
  children?: ReactNode;
}

const failureReasonKeys = {
  forbidden: "Server_Change_Failure_Forbidden",
  conflict: "Server_Change_Failure_Conflict",
  other: "Server_Change_Failure_Other",
  dependency: "Server_Change_Failure_Dependency",
} as const;

/**
 * Maps a typed review value to the copy shown in the Before or After column.
 *
 * @param value The typed value from the review descriptor.
 * @param t The translation function.
 * @returns The text, wrapped in {@link ApiText} when it originates from the API.
 */
function renderValue(value: ReviewValue, t: TFunction): ReactNode {
  switch (value.kind) {
    case "text":
      return value.apiText ? <ApiText>{value.text}</ApiText> : value.text;
    case "boolean":
      return value.value ? t("React_ServerWorkspace_Enabled") : t("React_ServerWorkspace_Disabled");
    case "notSet":
      return t("React_ServerWorkspace_NotSet");
    case "inherited":
      return t("Server_Change_Value_Inherited");
    case "secretChanged":
      return t("Server_Change_Value_SecretChanged");
    case "secretRotated":
      return t("Server_Change_Value_SecretRotated");
    case "noModPack":
      return t("Hosting_Create_ModPackNone");
    case "gameDefaults":
      return t("Server_Create_Preset_Default");
    case "profileLatest":
      return t("Server_Change_Value_ProfileLatest", { title: value.title });
    case "profilePinned":
      return t("Server_Change_Value_ProfilePinned", {
        title: value.title,
        revision: value.revision ?? "",
      });
  }
}

/**
 * One compact row of the review: the item name, the Before and After values with a decorative arrow, the outcome of
 * the last apply run under them and an icon-only control that removes the row from the staged changes.
 *
 * Credential rows show only the After value, because an existing credential is never described. The Before and
 * After terms stay in the description list for assistive technology but are visually hidden. Three columns above
 * 520px; the name above the values below.
 *
 * @param props The row, its optional outcome and removal callback, and optional extra content.
 */
export function ChangeDiffItem({ item, state, onRemove, children }: ChangeDiffItemProps) {
  const { t } = useTranslation();
  const isCredential = item.id.startsWith("secret:");
  const name =
    item.id === "modPack"
      ? t("Server_Change_Group_ModPack")
      : item.id === "profile"
        ? t("Server_Change_Group_Profile")
        : item.name;
  const nameNode = item.nameIsApiText ? <ApiText>{name}</ApiText> : name;
  return (
    <li className="change-item">
      <span className="change-item__name">{nameNode}</span>
      <div className="change-item__body">
        <div className="change-item__values">
          {!isCredential && (
            <>
              <dl className="change-item__pair change-item__pair--before">
                <dt className="sr-only">{t("Server_Change_Before")}</dt>
                <dd>{renderValue(item.before, t)}</dd>
              </dl>
              <ArrowRight16Regular className="change-item__arrow" aria-hidden="true" />
            </>
          )}
          <dl className="change-item__pair change-item__pair--after">
            <dt className="sr-only">{t("Server_Change_After")}</dt>
            <dd>{renderValue(item.after, t)}</dd>
          </dl>
        </div>
        {state?.state === "applied" && (
          <p className="change-item__state change-item__state--applied">
            <Checkmark16Regular aria-hidden="true" />
            <span>{t("Server_Change_State_Applied")}</span>
          </p>
        )}
        {state?.state === "failed" && (
          <p className="change-item__state change-item__state--failed">
            <Warning20Regular aria-hidden="true" />
            <span>{t("Server_Change_State_Failed")}</span>
            <span>{t(failureReasonKeys[state.reason])}</span>
          </p>
        )}
        {state?.state === "notTried" && (
          <p className="change-item__state">
            <span>{t("Server_Change_State_NotTried")}</span>
            <span>{t(failureReasonKeys[state.reason])}</span>
          </p>
        )}
        {children}
      </div>
      {onRemove && (
        <ActionButton
          variant="ghost"
          size="compact"
          iconOnly
          className="change-item__remove"
          aria-label={t("Server_Change_Item_Remove_Name", { name })}
          data-remove-item={item.id}
          onClick={onRemove}
        >
          <Dismiss16Regular aria-hidden="true" />
        </ActionButton>
      )}
    </li>
  );
}
