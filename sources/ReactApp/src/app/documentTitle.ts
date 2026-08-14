import { useEffect } from "react";

export function useDocumentTitle(title: string | undefined): void {
  useEffect(() => {
    if (title) document.title = title;
  }, [title]);
}
