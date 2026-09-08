import { eventSidePresentation, type EventSideId } from "@/features/events/eventSlotPresentation";

function SideGeometry({ id }: { id: EventSideId }) {
  if (id === "east") return <path d="M12 2 22 12 12 22 2 12 12 2Zm0 5-5 5 5 5 5-5-5-5Z" />;
  if (id === "resistance") return <path d="m12 2 10 18H2L12 2Zm0 6.2L7.8 16h8.4L12 8.2Z" />;
  if (id === "civilian") return <circle cx="12" cy="12" r="8" />;
  if (id === "ambient")
    return (
      <path d="M19.8 4.2C11 4.5 5.4 8.4 5.4 14.1c0 1.2.3 2.3.9 3.2l-2.1 2.1 1.4 1.4 2.1-2.1c.9.6 2 .9 3.2.9 5.7 0 9.6-5.6 9.9-14.4l-1-1Zm-8.9 11.4c-.5 0-1-.1-1.4-.3 2-2.1 4.4-4 7.2-5.6-1.6 2.8-3.5 5.2-5.8 5.9Z" />
    );
  if (id === "logic")
    return (
      <path d="M3 3h7v7H3V3Zm11 0h7v7h-7V3ZM3 14h7v7H3v-7Zm11 0h7v7h-7v-7Zm-9-9v3h3V5H5Zm11 0v3h3V5h-3ZM5 16v3h3v-3H5Zm11 0v3h3v-3h-3Z" />
    );
  if (id === "empty") return <path d="M3 3h18v18H3V3Zm2 2v14h14V5H5Z" />;
  return <path d="M3 4h18v16H3V4Zm3 3 6 4 6-4v3l-4 2 4 2v3l-6-4-6 4v-3l4-2-4-2V7Z" />;
}

export function EventSideMark({
  side,
  className,
}: {
  side: string | number | null | undefined;
  className?: string;
}) {
  const presentation = eventSidePresentation(side);
  return (
    <span
      className={["event-side-mark", className].filter(Boolean).join(" ")}
      data-side={presentation.id}
      aria-hidden="true"
    >
      <svg viewBox="0 0 24 24" focusable="false">
        <SideGeometry id={presentation.id} />
      </svg>
    </span>
  );
}
