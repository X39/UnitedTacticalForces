import { fileURLToPath, URL } from "node:url";
import react from "@vitejs/plugin-react";
import { defineConfig, loadEnv } from "vite";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const apiTarget =
    env.UTF_API_TARGET ||
    process.env.services__api__https__0 ||
    process.env.services__api__http__0 ||
    "https://localhost:7072";
  const classicTarget =
    env.UTF_CLASSIC_TARGET ||
    process.env.services__app__https__0 ||
    process.env.services__app__http__0 ||
    "https://localhost:7086";

  return {
    base: "/next/",
    plugins: [react()],
    resolve: {
      alias: {
        "@": fileURLToPath(new URL("./src", import.meta.url)),
      },
    },
    server: {
      port: 5173,
      strictPort: true,
      fs: {
        strict: true,
        allow: [
          fileURLToPath(new URL(".", import.meta.url)),
          fileURLToPath(new URL("../Common/Ui", import.meta.url)),
        ],
        deny: ["**/appsettings.secret.json", "**/appsettings.user.json", "**/.env*"],
      },
      proxy: {
        "/api": {
          target: apiTarget,
          changeOrigin: false,
          secure: false,
          ws: true,
          rewrite: (path) => path.replace(/^\/api/, ""),
          configure(proxy) {
            proxy.on("proxyReq", (proxyRequest, request) => {
              const forwardedProtocol = request.headers["x-forwarded-proto"] ?? "http";
              proxyRequest.setHeader("X-Forwarded-Proto", forwardedProtocol);
            });
          },
        },
        "/appsettings.json": {
          target: classicTarget,
          changeOrigin: false,
          secure: false,
        },
        "/signin-steam": {
          target: apiTarget,
          changeOrigin: false,
          secure: false,
          configure(proxy) {
            proxy.on("proxyReq", (proxyRequest, request) => {
              proxyRequest.setHeader(
                "X-Forwarded-Proto",
                request.headers["x-forwarded-proto"] ?? "http",
              );
            });
          },
        },
        "/signin-discord": {
          target: apiTarget,
          changeOrigin: false,
          secure: false,
          configure(proxy) {
            proxy.on("proxyReq", (proxyRequest, request) => {
              proxyRequest.setHeader(
                "X-Forwarded-Proto",
                request.headers["x-forwarded-proto"] ?? "http",
              );
            });
          },
        },
        "/media": { target: classicTarget, changeOrigin: false, secure: false },
        "/gdpr-privacy-hero.png": {
          target: classicTarget,
          changeOrigin: false,
          secure: false,
        },
        "/Insignia-48px.png": { target: classicTarget, changeOrigin: false, secure: false },
        "/insignia-48px.png": { target: classicTarget, changeOrigin: false, secure: false },
        "^/(?!next(?:/|$)|api(?:/|$))": {
          target: classicTarget,
          changeOrigin: false,
          secure: false,
          ws: true,
        },
      },
    },
    preview: {
      proxy: {
        "^/(?!next(?:/|$)|api(?:/|$))": {
          target: classicTarget,
          changeOrigin: false,
          secure: false,
        },
      },
    },
    build: {
      outDir: "dist",
      sourcemap: true,
      manifest: true,
    },
  };
});
