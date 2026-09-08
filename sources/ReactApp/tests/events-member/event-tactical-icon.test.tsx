import { render, screen } from "@testing-library/react";
import {
  EventTacticalIcon,
  eventMissionIconName,
  eventMissionIconNames,
  eventRoleIconName,
} from "@/features/events/EventTacticalIcon";
import { eventSidePresentation } from "@/features/events/eventSlotPresentation";

describe("event tactical presentation", () => {
  it("renders every supported mission symbol through the reusable SVG control", () => {
    for (const name of eventMissionIconNames) {
      const { unmount } = render(<EventTacticalIcon name={name} />);
      const icon = document.querySelector(`[data-tactical-icon="${name}"]`);
      expect(icon).toBeInTheDocument();
      expect(icon?.querySelector("svg")).toBeInTheDocument();
      unmount();
    }

    expect(eventMissionIconName(" RADIO ")).toBe("radio");
    expect(eventMissionIconName("unsupported-symbol")).toBe("objective");
  });

  it.each([
    ["Rifleman", "rifleman"],
    ["Grenadier", "grenadier"],
    ["Automatic Rifleman", "automatic-rifleman"],
    ["AT Rifleman", "anti-armor"],
    ["Team Lead", "host"],
    ["Sanitäter", "medical"],
    ["Funker", "radio"],
    ["Scharfschütze", "stealth"],
    ["Pionier EOD", "engineer"],
    ["Vehicle Crew", "crew"],
    ["Pilot", "pilot"],
    ["Ammo bearer", "support"],
    ["Mystery role", "slot"],
  ] as const)("maps the %s role to the %s symbol", (title, expected) => {
    expect(eventRoleIconName(title)).toBe(expected);
  });

  it("normalizes side identity for labels, ordering, and lower-case style hooks", () => {
    expect(eventSidePresentation("West")).toEqual({ id: "west", labelKey: "West", order: 0 });
    expect(eventSidePresentation(1)).toEqual({ id: "east", labelKey: "East", order: 1 });
    expect(eventSidePresentation("AmbientLife")).toEqual({
      id: "ambient",
      labelKey: "Ambient",
      order: 4,
    });
    expect(eventSidePresentation("Empty")).toEqual({
      id: "empty",
      labelKey: undefined,
      order: 6,
    });
  });

  it("marks tactical symbols as decorative when surrounding text names their meaning", () => {
    render(<EventTacticalIcon name="rifleman" className="test-symbol" />);

    const icon = screen.getByTestId("fluent-icon").parentElement;
    expect(icon).toHaveClass("event-tactical-icon", "test-symbol");
    expect(icon).toHaveAttribute("aria-hidden", "true");
  });
});
