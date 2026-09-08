import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { I18nextProvider } from "react-i18next";
import i18n, { i18nReady } from "@/app/i18n";
import { EventSlotPlanner } from "@/features/admin/EventSlotPlanner";
import type { EventSlotDraft } from "@/features/admin/eventAuthoring";

function slot(draftKey: string, side: EventSlotDraft["side"], title: string): EventSlotDraft {
  return {
    draftKey,
    slotNumber: draftKey === "west" ? 1 : 2,
    assignedToFk: "",
    originalAssignedToFk: "",
    assignedUser: null,
    assignmentCandidate: null,
    group: "Alpha",
    title,
    roleSymbol: title === "Medic" ? "Medic" : "Rifleman",
    side,
    isSelfAssignable: true,
    isVisible: true,
    isDeleted: false,
  };
}

describe("event slot planner", () => {
  beforeAll(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
  });

  it("separates same-named groups by side and presents role and side icons", async () => {
    const onChange = vi.fn();
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const initialSlots = [slot("west", "West", "Rifleman"), slot("east", "East", "Medic")];
    function Harness() {
      const [slots, setSlots] = useState(initialSlots);
      return (
        <EventSlotPlanner
          slots={slots}
          disabled={false}
          saving={false}
          onChange={(nextSlots) => {
            onChange(nextSlots);
            setSlots(nextSlots);
          }}
          onSave={vi.fn()}
          onAssign={vi.fn()}
          onUnassign={vi.fn()}
        />
      );
    }
    const { container } = render(
      <I18nextProvider i18n={i18n}>
        <QueryClientProvider client={queryClient}>
          <Harness />
        </QueryClientProvider>
      </I18nextProvider>,
    );

    const west = container.querySelector<HTMLElement>('[data-side="west"].event-slot-side-section');
    const east = container.querySelector<HTMLElement>('[data-side="east"].event-slot-side-section');
    expect(west).not.toBeNull();
    expect(east).not.toBeNull();
    expect(within(west!).getByRole("heading", { name: "Alpha" })).toBeVisible();
    expect(within(east!).getByRole("heading", { name: "Alpha" })).toBeVisible();
    expect(west!.querySelector('[data-tactical-icon="rifleman"]')).toBeInTheDocument();
    expect(east!.querySelector('[data-tactical-icon="medical"]')).toBeInTheDocument();
    expect(container.querySelector("select")).not.toBeInTheDocument();

    const westSymbolPicker = west!.querySelector<HTMLDetailsElement>(
      "details.event-role-symbol-picker",
    );
    expect(westSymbolPicker).not.toBeNull();
    await userEvent.click(westSymbolPicker!.querySelector("summary")!);
    await userEvent.click(within(westSymbolPicker!).getByRole("radio", { name: "Medic" }));
    expect(onChange).toHaveBeenLastCalledWith([
      expect.objectContaining({ draftKey: "west", roleSymbol: "Medic" }),
      expect.objectContaining({ draftKey: "east", roleSymbol: "Medic" }),
    ]);
    expect(
      west!.querySelector(".event-slot-editor-row__identity > .event-tactical-icon"),
    ).toHaveAttribute("data-tactical-icon", "medical");

    const groupInput = within(west!).getByRole("textbox", { name: "Group" });
    await userEvent.clear(groupInput);
    await userEvent.type(groupInput, "Bravo");
    expect(groupInput).toHaveValue("Bravo");
    await userEvent.tab();
    expect(onChange).toHaveBeenCalledWith([
      expect.objectContaining({ draftKey: "west", group: "Bravo" }),
      expect.objectContaining({ draftKey: "east", group: "Alpha" }),
    ]);

    await userEvent.click(within(west!).getByRole("button", { name: "East" }));
    expect(onChange).toHaveBeenCalledWith([
      expect.objectContaining({ draftKey: "west", side: "East" }),
      expect.objectContaining({ draftKey: "east", side: "East" }),
    ]);
    expect(screen.getAllByRole("button", { name: "West" })).toHaveLength(2);
  });
});
