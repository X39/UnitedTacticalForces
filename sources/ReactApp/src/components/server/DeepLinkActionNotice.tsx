import { Dismiss20Regular } from "@fluentui/react-icons";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { StatePanel } from "@/components/primitives";
import { ActionUnavailableReason } from "@/components/server/ActionUnavailableReason";
import { lookupActionVocabulary } from "@/features/hosting/actionVocabulary";
import type { GameServer, ServerAction } from "@/features/hosting/types";

/** Number of code points of an untrusted action id that the notice shows. */
const maxShownIdLength = 40;

/** Props of {@link DeepLinkActionNotice}. */
export type DeepLinkActionNoticeProps =
  | {
      /** The linked action exists but cannot be confirmed right now. */
      kind: "unavailable";
      server: GameServer;
      action: ServerAction;
      onDismiss(): void;
    }
  | {
      /** The linked action id does not exist on the server. */
      kind: "unknown";
      /** The raw, untrusted `action` query parameter. */
      actionId: string;
      onDismiss(): void;
    };

/**
 * Truncates an untrusted id to a fixed number of code points.
 *
 * @param id The raw id.
 * @returns The id itself, or its first code points followed by an ellipsis when it was longer.
 */
function truncateId(id: string): string {
  const points = Array.from(id);
  return points.length > maxShownIdLength ? `${points.slice(0, maxShownIdLength).join("")}…` : id;
}

/**
 * Explains an `?action=` deep link that cannot open a confirmation dialog.
 *
 * The notice never takes focus, never builds a link from the id and renders the untrusted id as a React text
 * child only. Unavailable actions explain their reason through the shared reason component.
 */
export function DeepLinkActionNotice(props: DeepLinkActionNoticeProps) {
  const { t } = useTranslation();
  const dismiss = (
    <ActionButton
      iconOnly
      variant="ghost"
      aria-label={t("Server_DeepLink_Dismiss")}
      onClick={props.onDismiss}
    >
      <Dismiss20Regular aria-hidden="true" />
    </ActionButton>
  );

  if (props.kind === "unknown") {
    return (
      <StatePanel kind="info" action={dismiss}>
        {t("Server_DeepLink_Unknown", { id: truncateId(props.actionId) })}
      </StatePanel>
    );
  }

  const vocabulary = lookupActionVocabulary(props.action.id);
  const name = vocabulary ? t(vocabulary.label) : props.action.id;
  return (
    <StatePanel kind="warning" action={dismiss}>
      {t("Server_DeepLink_Unavailable", { action: name })}{" "}
      <ActionUnavailableReason server={props.server} action={props.action} />
    </StatePanel>
  );
}
