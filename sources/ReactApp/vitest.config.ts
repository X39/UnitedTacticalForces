import { fileURLToPath, URL } from "node:url";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@fluentui/react-icons": fileURLToPath(
        new URL("./tests/mocks/react-icons.tsx", import.meta.url),
      ),
      "@fluentui/react-components": fileURLToPath(
        new URL("./tests/mocks/react-components.tsx", import.meta.url),
      ),
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  test: {
    globals: true,
    environment: "jsdom",
    setupFiles: ["./tests/setup.ts"],
    exclude: ["tests/e2e/**", "tests/uat/**", "node_modules/**", "dist/**"],
    css: true,
    coverage: { reporter: ["text", "json", "html"] },
  },
});
