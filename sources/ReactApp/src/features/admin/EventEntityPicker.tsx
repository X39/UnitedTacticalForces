import { useQuery } from "@tanstack/react-query";
import { useId, useState } from "react";
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
  const query = useQuery({
    queryKey: ["event-authoring", "selector", queryKey, search.trim()],
    queryFn: ({ signal }) => load(search.trim(), signal),
    enabled: open && !disabled,
    staleTime: 20_000,
  });
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
          onFocus={() => setOpen(true)}
          onBlur={() =>
            window.setTimeout(() => {
              setOpen(false);
              setSearch(selectedLabel);
            }, 120)
          }
          onChange={(event) => {
            setSearch(event.target.value);
            setOpen(true);
          }}
          onKeyDown={(event) => {
            if (event.key === "Escape") setOpen(false);
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
              setOpen(false);
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
              query.data.map((item) => (
                <button
                  key={getKey(item)}
                  type="button"
                  role="option"
                  aria-selected={selected ? getKey(item) === getKey(selected) : false}
                  onMouseDown={(event) => event.preventDefault()}
                  onClick={() => {
                    onSelect(item);
                    setSearch(getLabel(item));
                    setOpen(false);
                  }}
                >
                  <strong>{getLabel(item)}</strong>
                </button>
              ))
            )}
          </div>
        )}
      </div>
    </div>
  );
}
