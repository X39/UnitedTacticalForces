---
phase: 69-consistency-accessibility-verification
plan: 40
subsystem: ui
tags: [locales, l10n, german, check-locales, gap-closure]
requires:
  - phase: 69-consistency-accessibility-verification
    provides: "69-36 locale gate with [identical-case] and [english-words]"
provides:
  - "[english-carryover] rule in locales:check with the --shared-words option"
  - "shared-words-allowed.json, 122 documented words German shares with English"
  - "German values for the nine VERIFICATION gap strings, the two credential hints and the IN-06 values"
affects: [69-verification, l10n]
status: complete
commits: 2
plan_head_before: 5efa7c7e31ed7c20693e4e764a9631d254b4e90f
plan_head_after: ce56b9ea5397f16002304e8dc7cc02166b6fad97
actuals:
  tokens: 9000
  tasks: 2
  commits: 2
tech-stack:
  added: []
  patterns:
    - "A German locale value that repeats a content word of its English value fails the build unless the word is documented with one of five fixed reasons"
key-files:
  created:
    - sources/ReactApp/src/locales/shared-words-allowed.json
  modified:
    - sources/ReactApp/scripts/check-locales.mjs
    - sources/ReactApp/tests/locales/check-locales.test.ts
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/identical-allowed.json
key-decisions:
  - "Terminology: mod pack is Mod-Paket, terrain is Karte, assets is Material for the values this plan writes (UI-SPEC nav labels)"
  - "The three dead legacy keys (Manage Event, Decision reason, Provider state) are deleted after a grep proof of no quoted reference in sources/ReactApp/src"
requirements-completed: [L10N-03]
coverage:
  - id: D1
    description: "locales:check fails a German value that repeats an English word of its source unless the word is documented as shared"
    requirement: "L10N-03"
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/locales/check-locales.test.ts#fails on a German value that repeats an English word of its source"
        status: pass
    human_judgment: false
  - id: D2
    description: "The nine gap values, credential hints and IN-06 values read as German; real files pass with 0 failures and 0 warnings"
    requirement: "L10N-03"
    verification:
      - kind: other
        ref: "npm --prefix sources/ReactApp run locales:check"
        status: pass
    human_judgment: false
duration: 25min
completed: 2026-11-25
---

# Phase 69 Plan 40: German carry-over gate and half-English values Summary

The locale gate now fails any German value that repeats a content word of its own English value (`[english-carryover]`) unless the word is documented in `shared-words-allowed.json`, and every real hit on the converged pages is translated.

## Accomplishments

- `scripts/check-locales.mjs`: new `--shared-words` option, helpers `wordText`, `wordsOf`, `carriedOverWords`, constant `germanWordsLikeEnglish` (moved out of the `englishFunctionWords` JSDoc), the `[english-carryover]` rule, validation of the shared-word list (`[allow-reason]`, `[allow-stale]`), and the english-words allow list now exempting a key from both English-word rules.
- `shared-words-allowed.json` holds the 122 planned words, each with one of the five fixed reasons.
- de.json: the nine gap strings (Veranstaltungssteuerung, Missionsbild ausstehend, Details der ausgewählten Einstellung, Termin ausstehend, Bestätigtes Mitglied x2, Schritte zum Erstellen der Veranstaltung, Verknüpfte Konten, Mod-Pakete suchen), both credential hints ("In der Prüfung"), and the two IN-06 values of Task 1.
- Task 2 (IN-06): `Command-Center-Navigation` and `Command-Center-Navigation schließen`, `Initiale Slots`, `Karte, Mod-Paket und Bild` and `... Missionsbild`, `Kontostatus`, `Neueste Revision`, `Server erstellen`, `Kostenträger`; the now-stale `Shared_CommandCenter_NavigationAria` entry left identical-allowed.json.
- Dead keys `Manage Event`, `Decision reason` and `Provider state` removed from en.json and de.json (3900 to 3897 keys). Grep proof before deletion: no quoted reference (double, single, backtick) in `sources/ReactApp/src`.

## Verification

- `locales:check`: "Checked 3897 English and 3897 German keys: 0 failure(s), 0 warning(s)."
- `npm test -- tests/locales tests/hosting/server-section-entry.test.tsx`: 7 files, 173 tests passed.
- `npm run lint` and `npm run typecheck`: clean. `locales:unused`: exit 0 (only pre-existing `[unused-warn]` lines).
- With the shared list loaded, the pre-change run reported exactly the 16 expected keys (13 translated, 3 deleted); no word outside the 122 planned words appeared, so no word was added beyond the plan.
- RED was not demonstrated as a separate run: the new tests and the rule were written in the same working session, and the tests assert the exact failure lines (for example `[english-carryover] Pending - German value repeats English words: image`) against the real script.

## Deviations from Plan

None - plan executed as written. `node_modules` was absent, so `npm ci` restored the lockfile (no package added), as step 6 allows.

## Terminology decision and remaining counts (IN-06)

Settled: Mod-Paket, Karte, Material. Remaining spellings in de.json after this plan (lines containing them): "Modpack" 35, "Modpaket" 16, "Mod-Paket" 34, "Gelände" 43, "Karte" 33. Aligning those is outside this plan.

## Cross-plan findings

- de.json still mixes "Modpack"/"Modpaket"/"Mod-Paket" and "Gelände"/"Karte" outside the values IN-06 names; a follow-up terminology pass would need the same checker run.
- Full-suite Vitest was not run (documented WR-07 load timeouts); only the locales files and `server-section-entry` were run, all green.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

- shared-words-allowed.json exists and holds 122 keys; 88946e7c and ce56b9ea are ancestors of HEAD.
