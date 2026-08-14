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
}) {
  const id = useId();
  const [search, setSearch] = useState(selected ? getLabel(selected) : "");
  const [open, setOpen] = useState(false);
  const query = useQuery({
    queryKey: ["event-authoring", "selector", queryKey, search.trim()],
    queryFn: ({ signal }) => load(search.trim(), signal),
    enabled: open && !disabled,
    staleTime: 20_000,
  });

  return (
    <label className="event-authoring-picker" htmlFor={`${id}-input`}>
      <span>{label}</span>
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
              setSearch(selected ? getLabel(selected) : "");
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
    </label>
  );
}
