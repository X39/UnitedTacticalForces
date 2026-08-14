import type { paths } from "@/generated/api";

export type ApiPath = keyof paths;
export type ApiMethod<Path extends ApiPath> = Extract<
  keyof paths[Path],
  "get" | "post" | "put" | "patch" | "delete"
>;

export function apiPath<Path extends ApiPath>(path: Path): Path {
  return path;
}

export function apiPathWithQuery<Path extends ApiPath>(
  path: Path,
  query: string,
): `${Path}?${string}` {
  return `${path}?${query}`;
}

export function fillApiPath<Path extends ApiPath>(
  path: Path,
  parameters: Record<string, string | number>,
): string {
  return Object.entries(parameters).reduce(
    (resolved, [name, value]) => resolved.replace(`{${name}}`, encodeURIComponent(String(value))),
    path as string,
  );
}

export function apiMethod<Path extends ApiPath, Method extends ApiMethod<Path>>(
  _path: Path,
  method: Method,
): Uppercase<Method> {
  return method.toUpperCase() as Uppercase<Method>;
}
