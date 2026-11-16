import type { HTMLAttributes, ReactNode, SelectHTMLAttributes, ChangeEvent } from "react";

export function Spinner(props: HTMLAttributes<HTMLSpanElement>) {
  return <span role="status" {...props} />;
}

export function FluentProvider({ children }: { children: ReactNode }) {
  return children;
}

export const webDarkTheme = {};

export function Select({
  onChange,
  ...props
}: Omit<SelectHTMLAttributes<HTMLSelectElement>, "onChange"> & {
  onChange?(event: ChangeEvent<HTMLSelectElement>, data: { value: string }): void;
}) {
  return (
    <select {...props} onChange={(event) => onChange?.(event, { value: event.target.value })} />
  );
}
