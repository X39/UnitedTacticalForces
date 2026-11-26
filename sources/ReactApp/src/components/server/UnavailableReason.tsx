import { useTranslation } from "react-i18next";
import { resolveUnavailableReason } from "@/components/server/unavailableReasonMapping";

/**
 * Shows why an action is unavailable as visible, localized text.
 *
 * Known API reasons become localized sentences. Unknown API text follows a generic sentence inside a `q`
 * element marked `lang="en"`, because API prose is English. All text is rendered as React text children, never
 * as HTML.
 *
 * @param props.reason The reason text sent by the API.
 * @param props.stateLabel Localized label of the current server state for the deployment-state reason.
 * @param props.launchDisabled Whether the server is known to be launch-disabled.
 * @param props.id Optional element id so a control can link the reason with `aria-describedby`.
 */
export function UnavailableReason({
  reason,
  stateLabel,
  launchDisabled,
  id,
}: {
  reason: string | null | undefined;
  stateLabel?: string | undefined;
  launchDisabled?: boolean | undefined;
  id?: string | undefined;
}) {
  const { t } = useTranslation();
  const resolved = resolveUnavailableReason(reason, { stateLabel, launchDisabled });
  return (
    <span id={id}>
      {resolved.values ? t(resolved.key, resolved.values) : t(resolved.key)}
      {resolved.quoted !== undefined && (
        <>
          {" "}
          <q lang="en">{resolved.quoted}</q>
        </>
      )}
    </span>
  );
}
