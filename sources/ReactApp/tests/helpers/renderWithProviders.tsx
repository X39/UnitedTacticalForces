import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, type RenderResult } from "@testing-library/react";
import { StrictMode, type ReactElement } from "react";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider, type DataRouter } from "react-router";
import i18n from "@/app/i18n";
import { AppToastProvider } from "@/components/AppToasts";
import { CostConfirmationProvider } from "@/components/server/CostConfirmationProvider";

/** Options of {@link renderWithProviders}. */
export interface RenderWithProvidersOptions {
  /** Initial router entry, including any search string. Defaults to "/". */
  route?: string;
  /** Route path pattern the UI is mounted on. Defaults to a catch-all so navigation keeps the UI mounted. */
  path?: string;
  /** Query client to use; a fresh client without retries is created when omitted. */
  queryClient?: QueryClient;
  /** Wraps the whole tree in React StrictMode. */
  strict?: boolean;
}

/** The render result plus the router and query client the UI runs under. */
export type RenderWithProvidersResult = RenderResult & {
  router: DataRouter;
  queryClient: QueryClient;
};

/**
 * Renders UI under the providers the route layouts supply: query client, i18n, toasts, a memory router and the
 * cost confirmation provider.
 *
 * @param ui The element to render inside the confirmation provider.
 * @param options Route, path, query client and StrictMode options.
 */
export function renderWithProviders(
  ui: ReactElement,
  options: RenderWithProvidersOptions = {},
): RenderWithProvidersResult {
  const queryClient =
    options.queryClient ??
    new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  const router = createMemoryRouter(
    [
      {
        path: options.path ?? "*",
        element: <CostConfirmationProvider>{ui}</CostConfirmationProvider>,
      },
    ],
    { initialEntries: [options.route ?? "/"] },
  );
  const tree = (
    <QueryClientProvider client={queryClient}>
      <I18nextProvider i18n={i18n}>
        <AppToastProvider>
          <RouterProvider router={router} />
        </AppToastProvider>
      </I18nextProvider>
    </QueryClientProvider>
  );
  const result = render(options.strict ? <StrictMode>{tree}</StrictMode> : tree);
  return Object.assign(result, { router, queryClient });
}
