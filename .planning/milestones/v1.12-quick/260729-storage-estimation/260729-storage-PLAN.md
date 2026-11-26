---
quick_id: 260729-storage
status: complete
description: Implement pre-deployment storage estimation and worker capacity validation
---

# Quick Task 260729-storage Plan

## Task 1: Persist and calculate storage demand

- Add provider-neutral demand/observation models, conservative policy configuration, Steam Workshop metadata refresh, and an EF migration.
- Reuse the existing effective Arma 3 content resolver and measure persistent server files safely.

## Task 2: Enforce the worker boundary

- Snapshot demand into Start commands, report controlled-root capacity, block SteamCMD on insufficient mounted storage, and persist replay-safe installed-size observations.

## Task 3: Expose and verify the feature

- Add authorized API/UI projections, creation-time Modpack selection, accessible profile warnings, active-spec documentation, and focused/full verification.
