# Deferred items found by 69-03

Out-of-scope discoveries logged while enumerating strings for Wave 2. None of these is fixed by 69-03.

## Text-keyed lookups with no locale entry (70 sites, all in mod pack admin pages)

`t("English sentence")` calls whose argument is not a key in `en.json` or `de.json`. i18next returns the key, so the
English sentence renders in German. Found by a read-only scan of every `t("...")` first argument against `en.json`:
99 distinct unresolved first arguments, of which 29 are `Server_` plural bases (they resolve through `_one` and
`_other` variants and are not defects) and 70 are real misses.

- All 70 are in `ModPackContentEditor.tsx`, `ModPackCreatePage.tsx` and `ModPackWorkspacePage.tsx`, for example
  `t("Mods")`, `t("Review sources")`, `t("Creating…")`, `t("Immutable")`.
- Five of them are covered by 69-03 because the plan names them or they are empty-state text: the tab labels at
  `ModPackWorkspacePage.tsx:248` (`Content`, `Danger`) and the four empty states at `ModPackCreatePage.tsx:102` and
  `ModPackWorkspacePage.tsx:474`, `512`, `553`. See the SUMMARY key map.
- The other 65 are Tier B light-pass work and need a keyed rewrite of the three files. They are outside the L10N-03
  static scan of Tier A files, but a German walk of any mod pack screen would flag them.

## Error and forbidden StatePanels with a dynamic `kind`

Six `StatePanel` elements choose `kind` at run time (`TerrainCreatePage:127`, `TerrainsPage:167`, `WikiPage:382`,
`536`, `739`, `HomePage:192`). They are error, forbidden or warning panels, not empty panels, so 69-03 adds no empty
title for them. Wave 2 converts them to `ErrorState` and `ForbiddenState`, which already carry generic
`Common_State_*` keys.
