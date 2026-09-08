import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState, type ReactNode } from "react";
import { EventEntityPicker } from "@/features/admin/EventEntityPicker";

interface PickerOption {
  id: string;
  label: string;
  detail: string;
}

const options: PickerOption[] = [
  { id: "alpha", label: "Alpha", detail: "Squad lead" },
  { id: "bravo", label: "Bravo", detail: "Medic" },
];

function renderPicker({
  load = vi.fn().mockResolvedValue(options),
  onSelect = vi.fn(),
  renderOption,
}: {
  load?: (search: string, signal?: AbortSignal) => Promise<PickerOption[]>;
  onSelect?: (item: PickerOption) => void;
  renderOption?: (item: PickerOption) => ReactNode;
} = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  function Harness() {
    const [selected, setSelected] = useState<PickerOption | null>(null);
    return (
      <EventEntityPicker
        label="Member"
        placeholder="Search members"
        selected={selected}
        queryKey="members"
        load={load}
        getKey={(item) => item.id}
        getLabel={(item) => item.label}
        {...(renderOption ? { renderOption } : {})}
        emptyLabel="No members"
        loadingLabel="Loading members"
        errorLabel="Members failed"
        onSelect={(item) => {
          setSelected(item);
          onSelect(item);
        }}
      />
    );
  }
  render(
    <QueryClientProvider client={queryClient}>
      <Harness />
    </QueryClientProvider>,
  );
  return { load, onSelect };
}

describe("event entity picker", () => {
  it("renders rich option content supplied by a caller", async () => {
    const user = userEvent.setup();
    renderPicker({
      renderOption: (item) => (
        <span data-testid={`option-${item.id}`}>
          <strong>{item.label}</strong>
          <small>{item.detail}</small>
        </span>
      ),
    });

    await user.click(screen.getByRole("combobox", { name: "Member" }));

    expect(await screen.findByTestId("option-alpha")).toHaveTextContent("AlphaSquad lead");
    expect(screen.getByTestId("option-bravo")).toHaveTextContent("BravoMedic");
  });

  it("tracks, wraps, and selects the active option from the keyboard", async () => {
    const user = userEvent.setup();
    const { onSelect } = renderPicker();
    const input = screen.getByRole("combobox", { name: "Member" });
    await user.click(input);
    const renderedOptions = await screen.findAllByRole("option");

    expect(input).not.toHaveAttribute("aria-activedescendant");
    await user.keyboard("{ArrowDown}");
    expect(input).toHaveAttribute("aria-activedescendant", renderedOptions[0]?.id);
    expect(renderedOptions[0]).toHaveAttribute("aria-selected", "true");

    await user.keyboard("{ArrowDown}");
    expect(input).toHaveAttribute("aria-activedescendant", renderedOptions[1]?.id);
    await user.keyboard("{ArrowDown}");
    expect(input).toHaveAttribute("aria-activedescendant", renderedOptions[0]?.id);
    await user.keyboard("{ArrowUp}");
    expect(input).toHaveAttribute("aria-activedescendant", renderedOptions[1]?.id);

    await user.keyboard("{Enter}");
    expect(onSelect).toHaveBeenCalledWith(options[1]);
    expect(input).toHaveValue("Bravo");
    expect(input).toHaveAttribute("aria-expanded", "false");
    expect(input).not.toHaveAttribute("aria-activedescendant");
  });

  it("honors navigation begun while asynchronous options are still loading and escapes cleanly", async () => {
    const user = userEvent.setup();
    let resolveOptions: ((value: PickerOption[]) => void) | undefined;
    const load = vi.fn(
      () =>
        new Promise<PickerOption[]>((resolve) => {
          resolveOptions = resolve;
        }),
    );
    renderPicker({ load });
    const input = screen.getByRole("combobox", { name: "Member" });

    input.focus();
    await user.keyboard("{ArrowDown}");
    resolveOptions?.(options);
    const renderedOptions = await screen.findAllByRole("option");
    await waitFor(() =>
      expect(input).toHaveAttribute("aria-activedescendant", renderedOptions[0]?.id),
    );

    await user.keyboard("{Escape}");
    expect(input).toHaveAttribute("aria-expanded", "false");
    expect(input).not.toHaveAttribute("aria-activedescendant");
  });
});
