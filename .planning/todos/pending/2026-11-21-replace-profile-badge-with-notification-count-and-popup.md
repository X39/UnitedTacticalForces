---
created: 2026-11-21T18:11:20.691Z
title: Replace profile badge with notification count and popup
area: ui
severity: minor
files:
  - sources/ReactApp/src/features/hosting/CostAlertBanner.tsx
  - sources/ReactApp/src/features/hosting/costFrames.ts
audit_acknowledged:
  milestone: v2.1
  at: 2026-11-26
---

## Problem

Found in phase 66 UAT. The profile-menu avatar badge counts distinct servers that have cost alerts, so it acts as a server count rather than a notification count. A cost alert should raise the profile's notification count by one. Cost alerts also show as rows in the profile menu, which is a stopgap and not a proper notification surface.

## Solution

Fine as-is for now. In the long term, give the profile a real notification model: a count that rises with each new alert, and a proper notification popup that lists alerts and other notifications. Revisit the notification model as a whole, including the existing 15 s notifications poll.
