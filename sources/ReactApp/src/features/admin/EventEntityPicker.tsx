import { useQuery } from "@tanstack/react-query";
import { useEffect, useId, useState, type ReactNode } from "react";
import { StatePanel } from "@/components/primitives";

export function EventEntityPicker<T>({
  label,
  placeholder,
  selected,
  disabled = false,
  queryKey,
  load,
  getKey,
  getLabel,
  renderOption,
  emptyLabel,
  loadingLabel,
  errorLabel,
  onSelect,
  clearLabel,
  onClear,
}: {
  label: string;
  placeholder: string;
  selected: T | null;
  disabled?: boolean;
  queryKey: string;
  load(search: string, signal?: AbortSignal): Promise<T[]>;
  getKey(item: T): string;
  getLabel(item: T): string;
  renderOption?(item: T): ReactNode;
  emptyLabel: string;
  loadingLabel: string;
  errorLabel: string;
  onSelect(item: T): void;
  clearLabel?: string;
  onClear?(): void;
}) {
  const id = useId();
  const selectedKey = selected ? getKey(selected) : "";
  const selectedLabel = selected ? getLabel(selected) : "";
  const [searchState, setSearchState] = useState({ selectedKey, value: selectedLabel });
  const search = searchState.selectedKey === selectedKey ? searchState.value : selectedLabel;
  const setSearch = (value: string) => setSearchState({ selectedKey, value });
  const [open, setOpen] = useState(false);
  const [activeKey, setActiveKey] = useState<string>();
  const [pendingNavigation, setPendingNavigation] = useState<"first" | "last" | null>(null);
  const query = useQuery({
    queryKey: ["event-authoring", "selector", queryKey, search.trim()],
    queryFn: ({ signal }) => load(search.trim(), signal),
    enabled: open && !disabled,
    staleTime: 20_000,
  });
  const options = query.data ?? [];
  const pendingActive =
    !activeKey && pendingNavigation && options.length
      ? pendingNavigation === "last"
        ? options[options.length - 1]
        : options[0]
      : undefined;
  const resolvedActiveKey = activeKey ?? (pendingActive ? getKey(pendingActive) : undefined);
  const activeIndex = resolvedActiveKey
    ? options.findIndex((item) => getKey(item) === resolvedActiveKey)
    : -1;
  const activeOptionId = activeIndex >= 0 ? `${id}-option-${activeIndex}` : undefined;
  const close = (restoreSearch: boolean) => {
    setOpen(false);
    setActiveKey(undefined);
    setPendingNavigation(null);
    if (restoreSearch) setSearch(selectedLabel);
  };
  const select = (item: T) => {
    const itemKey = getKey(item);
    onSelect(item);
    setSearchState({ selectedKey: itemKey, value: getLabel(item) });
    close(false);
  };

  useEffect(() => {
    if (!activeOptionId) return;
    document.getElementById(activeOptionId)?.scrollIntoView?.({ block: "nearest" });
  }, [activeOptionId]);

  return (
    <div className="event-authoring-picker">
      <label htmlFor={`${id}-input`}>{label}</label>
      <div className="event-authoring-picker__control">
        <input
          id={`${id}-input`}
          value={search}
          placeholder={placeholder}
          disabled={disabled}
          role="combobox"
          autoComplete="off"
          aria-autocomplete="list"
          aria-controls={`${id}-options`}
          aria-expanded={open}
          aria-activedescendant={open ? activeOptionId : undefined}
          onFocus={() => setOpen(true)}
          onBlur={() =>
            window.setTimeout(() => {
              close(true);
            }, 120)
          }
          onChange={(event) => {
            setSearch(event.target.value);
            setOpen(true);
            setActiveKey(undefined);
            setPendingNavigation(null);
          }}
          onKeyDown={(event) => {
            if (event.key === "ArrowDown" || event.key === "ArrowUp") {
              event.preventDefault();
              const direction = event.key === "ArrowDown" ? "first" : "last";
              setOpen(true);
              if (!options.length) {
                setPendingNavigation(direction);
                return;
              }
              setPendingNavigation(null);
              const nextIndex =
                activeIndex < 0
                  ? direction === "first"
                    ? 0
                    : options.length - 1
                  : (activeIndex + (direction === "first" ? 1 : -1) + options.length) %
                    options.length;
              const next = options[nextIndex];
              if (next) setActiveKey(getKey(next));
              return;
            }
            if (event.key === "Enter" && open && activeIndex >= 0) {
              event.preventDefault();
              const active = options[activeIndex];
              if (active) select(active);
              return;
            }
            if (event.key === "Escape" && open) {
              event.preventDefault();
              close(true);
            }
          }}
        />
        {selected && onClear && (
          <button
            className="event-authoring-picker__clear"
            type="button"
            disabled={disabled}
            aria-label={clearLabel}
            title={clearLabel}
            onMouseDown={(event) => event.preventDefault()}
            onClick={() => {
              setSearch("");
              close(false);
              onClear();
            }}
          >
            <span aria-hidden="true">&times;</span>
          </button>
        )}
        {open && (
          <div className="event-authoring-picker__popover" id={`${id}-options`} role="listbox">
            {query.isPending ? (
              <span>{loadingLabel}</span>
            ) : query.isError ? (
              <StatePanel kind="error">{errorLabel}</StatePanel>
            ) : !query.data?.length ? (
              <span>{emptyLabel}</span>
            ) : (
              options.map((item, index) => {
                const itemKey = getKey(item);
                const isActive = itemKey === resolvedActiveKey;
                return (
                  <button
                    id={`${id}-option-${index}`}
                    key={itemKey}
                    type="button"
                    role="option"
                    aria-selected={isActive}
                    data-current={itemKey === selectedKey || undefined}
                    tabIndex={-1}
                    onMouseDown={(event) => event.preventDefault()}
                    onMouseEnter={() => {
                      setPendingNavigation(null);
                      setActiveKey(itemKey);
                    }}
                    onClick={() => select(item)}
                  >
                    {renderOption ? renderOption(item) : <strong>{getLabel(item)}</strong>}
                  </button>
                );
              })
            )}
          </div>
        )}
      </div>
    </div>
  );
}
