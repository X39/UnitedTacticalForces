import "@/components/server/reason-reserve.css";

/** Props of {@link ReasonReserve}. */
export interface ReasonReserveProps {
  /** Every reason the neighbouring action can show at some point, in the language of the page. */
  reasons: readonly string[];
}

/**
 * Reserves the room of the unavailable reason of an action, so the line that explains why the action is unavailable
 * can appear, change and disappear without moving anything below it.
 *
 * The component renders an invisible copy of every possible reason, stacked in one grid cell, so the tallest text at
 * the width of the cell sets the height whatever reason is shown. The caller places it in the same grid area as the
 * real reason, for example a full-width row under a form row. The copy is drawn from a `data-text` attribute through a
 * pseudo-element, so it adds no text to the document for tests or assistive technology to find, and it is hidden from
 * the accessibility tree.
 *
 * @param props.reasons Every reason the action can show.
 * @returns The invisible, space-reserving block.
 */
export function ReasonReserve({ reasons }: ReasonReserveProps) {
  return (
    <div className="reason-reserve" aria-hidden="true">
      {reasons.map((reason) => (
        <span key={reason} className="action-reason reason-reserve__reason">
          <span className="reason-reserve__icon" />
          <span className="reason-reserve__text" data-text={reason} />
        </span>
      ))}
    </div>
  );
}
