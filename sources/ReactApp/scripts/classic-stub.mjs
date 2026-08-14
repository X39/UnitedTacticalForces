import { createServer } from "node:http";
import { readFileSync } from "node:fs";

const port = Number(process.env.UTF_CLASSIC_STUB_PORT ?? 4174);
createServer((request, response) => {
  if (request.url === "/health") {
    response.writeHead(200, { "Content-Type": "text/plain" });
    response.end("ok");
    return;
  }
  if (request.url === "/appsettings.json") {
    response.writeHead(200, { "Content-Type": "application/json" });
    response.end(
      JSON.stringify({
        Api: { BaseUrl: "/api/" },
        Urls: { TeamSpeak3: "", Discord: "" },
        Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
      }),
    );
    return;
  }
  if (request.url === "/api/users/me") {
    response.writeHead(204);
    response.end();
    return;
  }
  if (request.url === "/api/users/authentication-providers") {
    response.writeHead(200, { "Content-Type": "application/json" });
    response.end(JSON.stringify({ steam: true, discord: true }));
    return;
  }
  if (request.url === "/api/site-image-settings" || request.url === "/api/events/upcoming") {
    response.writeHead(200, { "Content-Type": "application/json" });
    response.end("[]");
    return;
  }
  if (request.url?.startsWith("/api/field-reports")) {
    response.writeHead(200, { "Content-Type": "application/json" });
    response.end("[]");
    return;
  }
  if (
    request.url === "/Insignia-48px.png" ||
    request.url === "/insignia-48px.png" ||
    request.url === "/gdpr-privacy-hero.png" ||
    /^\/media\/(?:generated|field-reports)\/[a-z0-9.-]+$/i.test(request.url ?? "")
  ) {
    const assetPath = request.url === "/insignia-48px.png" ? "/Insignia-48px.png" : request.url;
    try {
      const bytes = readFileSync(new URL(`../../WebApp/wwwroot${assetPath}`, import.meta.url));
      response.writeHead(200, {
        "Content-Type": assetPath?.endsWith(".png") ? "image/png" : "image/jpeg",
      });
      response.end(bytes);
    } catch {
      response.writeHead(404);
      response.end();
    }
    return;
  }
  response.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
  response.end(
    '<!doctype html><html><body><main data-testid="classic-shell">Classic United Tactical Forces</main></body></html>',
  );
}).listen(port, "127.0.0.1");
