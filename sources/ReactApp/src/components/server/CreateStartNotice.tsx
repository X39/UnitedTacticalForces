import { Dismiss20Regular } from "@fluentui/react-icons";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { StatePanel } from "@/components/primitives";
import {
  ActionUnavailableReason,
  actionIsUnavailable,
} from "@/components/server/ActionUnavailableReason";
import "@/components/server/create.css";
import type { GameServer, ServerAction } from "@/features/hosting/types";

/** Invisible marker that stands in for the server title while the localized sentence is split around it. */
const nameMarker = String.fromCodePoint(0x2063);

/** Props of {@link CreateStartNotice}. */
export interface CreateStartNoticeProps {
  /** The server that was just created and is still off. */
  server: GameServer;
  /** The start action of the server, or undefined when the server has none. */
  startAction: ServerAction | undefined;
  /** Called when the member dismisses the notice. */
  onDismiss(): void;
}

/**
 * Explains a created server whose start was not confirmed (D-07).
 *
 * The notice is a polite status panel for the workspace notice slot. The server title is API-originated text and
 * renders only as a React text child inside a wrapping element; when the start action exists but cannot be
 * confirmed, the mapped unavailable reason follows the sentence. The notice never takes focus.
 */
export function CreateStartNotice({ server, startAction, onDismiss }: CreateStartNoticeProps) {
  const { t } = useTranslation();
  const [before = "", after = ""] = t("Server_Create_Notice_NotStarted", {
    name: nameMarker,
  }).split(nameMarker);
  return (
    <StatePanel
      kind="info"
      action={
        <ActionButton
          iconOnly
          variant="ghost"
          aria-label={t("Server_Create_Notice_Dismiss")}
          onClick={onDismiss}
        >
          <Dismiss20Regular aria-hidden="true" />
        </ActionButton>
      }
    >
      {before}
      <span className="create-notice__name">{server.title}</span>
      {after}
      {startAction !== undefined && actionIsUnavailable(startAction) ? (
        <>
          {" "}
          <ActionUnavailableReason server={server} action={startAction} />
        </>
      ) : null}
    </StatePanel>
  );
}
