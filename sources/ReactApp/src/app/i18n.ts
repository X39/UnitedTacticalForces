import i18n from "i18next";
import LanguageDetector from "i18next-browser-languagedetector";
import { initReactI18next } from "react-i18next";
import de from "@/generated/locales/de.json";
import en from "@/generated/locales/en.json";

const supportedLanguages = ["en", "de"] as const;

function synchronizeDocumentLanguage(language: string | undefined): void {
  document.documentElement.lang = language?.startsWith("de") ? "de" : "en";
}

export const i18nReady = i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: { en: { translation: en }, de: { translation: de } },
    fallbackLng: "en",
    supportedLngs: [...supportedLanguages],
    load: "languageOnly",
    interpolation: { escapeValue: false },
    detection: {
      order: ["localStorage", "navigator"],
      lookupLocalStorage: "UtfCulture",
      caches: ["localStorage"],
    },
    react: { useSuspense: false },
  })
  .then(() => synchronizeDocumentLanguage(i18n.resolvedLanguage));

i18n.on("languageChanged", synchronizeDocumentLanguage);

export function setCulture(language: (typeof supportedLanguages)[number]): void {
  localStorage.setItem("UtfCulture", language);
  void i18n.changeLanguage(language);
}

export function formatResource(value: string, ...values: Array<string | number>): string {
  let result = value;
  values.forEach((item, index) => {
    if (typeof item === "number") {
      const locale = i18n.resolvedLanguage?.startsWith("de") ? "de-DE" : "en-US";
      for (let digits = 0; digits <= 4; digits += 1) {
        const formatted = new Intl.NumberFormat(locale, {
          minimumFractionDigits: digits,
          maximumFractionDigits: digits,
        }).format(item);
        result = result.replaceAll(`{${index}:N${digits}}`, formatted);
      }
    }
    result = result.replaceAll(`{${index}}`, String(item));
  });
  return result;
}

export default i18n;
