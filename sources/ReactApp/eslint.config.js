import js from "@eslint/js";
import tseslint from "@typescript-eslint/eslint-plugin";
import tsparser from "@typescript-eslint/parser";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import globals from "globals";
import { readdirSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

const ratchetDirectory = fileURLToPath(new URL("./tests/ratchets/", import.meta.url));
const readRatchetJson = (name) => JSON.parse(readFileSync(`${ratchetDirectory}${name}`, "utf8"));
const rules = readRatchetJson("rules.json");
const families = readdirSync(ratchetDirectory)
  .filter((name) => /^family\..+\.json$/.test(name))
  .map(readRatchetJson);

// Files that still hold raw controls or button look-alikes are held by their family ceiling in
// tests/ratchets (enforced by Vitest) and are exempt here until their converting plan removes them.
const ratchetedFiles = families.flatMap((family) =>
  Object.entries(family.files)
    .filter(([, entry]) => entry.controls > 0 || entry.lookalikes > 0)
    .map(([path]) => path),
);

// One object per file group: a later object with the same rule key replaces an earlier one.
const lookalike = `/${rules.lookalikePattern}/`;
const restrictedControls = {
  files: ["src/**/*.tsx"],
  ignores: [...rules.primitiveFiles, ...ratchetedFiles],
  rules: {
    "no-restricted-syntax": [
      "error",
      {
        selector: `JSXOpeningElement[name.type='JSXIdentifier'][name.name=/^(${rules.elements.join("|")})$/]`,
        message:
          "Use the shared control: ActionButton, ActionLink or ActionAnchor, TextField, SelectField, Disclosure or ConfirmDialog.",
      },
      {
        selector: `JSXAttribute[name.name='className'] > Literal[value=${lookalike}]`,
        message:
          "Use ActionButton, ActionLink or ActionAnchor instead of a button look-alike class.",
      },
      {
        selector: `JSXAttribute[name.name='className'] > JSXExpressionContainer > Literal[value=${lookalike}]`,
        message:
          "Use ActionButton, ActionLink or ActionAnchor instead of a button look-alike class.",
      },
      {
        selector: `JSXAttribute[name.name='className'] > JSXExpressionContainer > TemplateLiteral > TemplateElement:first-child[value.raw=${lookalike}]`,
        message:
          "Use ActionButton, ActionLink or ActionAnchor instead of a button look-alike class.",
      },
    ],
  },
};

export default [
  { ignores: ["dist", "coverage", "playwright-report", "src/generated/**"] },
  js.configs.recommended,
  {
    files: ["*.config.{js,ts}", "scripts/**/*.mjs"],
    languageOptions: { globals: globals.node },
  },
  {
    files: ["**/*.{ts,tsx}"],
    languageOptions: {
      parser: tsparser,
      parserOptions: { ecmaVersion: "latest", sourceType: "module", projectService: true },
      globals: globals.browser,
    },
    plugins: {
      "@typescript-eslint": tseslint,
      "react-hooks": reactHooks,
      "react-refresh": reactRefresh,
    },
    rules: {
      ...tseslint.configs.recommended.rules,
      ...reactHooks.configs.flat.recommended.rules,
      "react-refresh/only-export-components": "off",
      "@typescript-eslint/no-explicit-any": "error",
    },
  },
  restrictedControls,
  {
    files: ["tests/**/*.test.{ts,tsx}"],
    languageOptions: {
      globals: {
        ...globals.node,
        describe: "readonly",
        it: "readonly",
        expect: "readonly",
        vi: "readonly",
        beforeAll: "readonly",
        afterEach: "readonly",
      },
    },
  },
];
