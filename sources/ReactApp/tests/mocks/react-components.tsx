import type { HTMLAttributes, ReactNode } from "react";

export function Spinner(props: HTMLAttributes<HTMLSpanElement>) {
  return <span role="status" {...props} />;
}

export function FluentProvider({ children }: { children: ReactNode }) {
  return children;
}

export const webDarkTheme = {};
