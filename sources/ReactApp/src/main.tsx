import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "@/app/App";
import { loadPublicConfiguration } from "@/app/config";
import i18n, { i18nReady } from "@/app/i18n";
import { initializeApiClient } from "@/api/client";
import { setUiVariant } from "@/app/routes";
import "highlight.js/styles/github-dark.css";
import "@/styles/tokens.css";
import "@/styles.css";

const rootElement = document.getElementById("root");
if (!rootElement) throw new Error("The React application root element is missing.");
const root = createRoot(rootElement);

async function bootstrap(): Promise<void> {
  try {
    await i18nReady;
    const configuration = await loadPublicConfiguration();
    initializeApiClient(configuration);
    root.render(
      <StrictMode>
        <App configuration={configuration} />
      </StrictMode>,
    );
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : "The public configuration is invalid.";
    root.render(
      <main className="bootstrap-error" role="alert">
        <img src="/Insignia-48px.png" alt="" />
        <p>UNITED TACTICAL FORCES</p>
        <h1>{i18n.t("React_Bootstrap_Title")}</h1>
        <p>{message}</p>
        <a href="/" onClick={() => setUiVariant("classic")}>
          {i18n.t("React_Bootstrap_Classic")}
        </a>
      </main>,
    );
  }
}

void bootstrap();
