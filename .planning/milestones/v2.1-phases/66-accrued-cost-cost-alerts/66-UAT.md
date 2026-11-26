---
phase: 66
status: complete
date: 2026-11-21
source: [66-VERIFICATION.md]
updated: 2026-11-22T19:55:14.802Z
---

# Phase 66 UAT

Human run of the end-of-phase UAT (`./scripts/Start-Uat.ps1 -Fresh`). Result in the user's words: pass, apart from the items below. The Discord DM step was skipped as optional.

## Gaps

All three gaps below are resolved by gap closure plans 66-13 to 66-18 (status: resolved) and re-checked by the automated seeded-stack UAT run on 2026-11-22.

1. **Accrued cost ignores provider billing granularity (correctness, CST-03).** The calculator prorates each charged slice by elapsed time. Billing differs per provider. Hetzner bills the started hour, so a session that has run 5 minutes has already incurred a full hour. The accrued value, and the threshold crossing that depends on it, must follow the provider's billing model. Needs a provider-neutral billing-granularity concept (for example a billing unit per provider) that the calculator uses, with Hetzner's started-hour rule as the first implementation.
2. **Cost-alert UI quality (UI).** The user finds the cost-alert UI ugly (banner, menu rows, settings page). Needs a design pass. No specific element was named; ask before redesigning.
3. **Alert dismissal unclear (to confirm).** The user could not tell whether the alert dismissed itself or they dismissed it by hand. Check that nothing auto-dismisses it (including a redelivered or invalidated alert list) and that a dismissal gives visible feedback.

## Recorded elsewhere

- The profile badge counts distinct servers instead of notifications. Recorded as todo `2026-11-21-replace-profile-badge-with-notification-count-and-popup`. Fine for now.

## Passed

Accrued amount advances, the alert arrived live, the stop-billing confirmation opened from the banner, email arrived, and no repeat after restart.

## Tests

Re-verification after gap closure 66-13 to 66-18 (2026-11-22). Real-environment re-checks are deferred to the milestone end.

### 1. Deferred follow-up: started-hour accrued value. Run ./scripts/Start-Uat.ps1 -Fresh and open 'UAT 9 Costing (accrued cost)' at http://localhost:5173/next/command/servers
expected: The accrued row shows about 1.20 EUR with 'Billed per started hour.' and the next-hour instant, and steps to 1.80 EUR when the third started hour begins about five minutes later; it does not creep per minute
result: pass
source: automated
evidence: "sources/ReactApp/tests/uat/cost-accrued.spec.ts on the real seeded stack (2026-11-22): 1.20 EUR (2 started hours x 0.60) stepped to 1.80 EUR exactly at the shown next-hour instant; stayed 1.80 for 90 s and across a reload with Billed per started hour."

### 2. Deferred follow-up: alert at the started hour. Save an alert amount equal to the hourly rate on the costing server (Set an alert)
expected: Within about 30 seconds a warning toast, the 'Cost alert' banner and the account menu row appear, with the full-hour amount; exactly one email appears in Mailpit (http://localhost:8025)
result: pass
source: automated
evidence: "sources/ReactApp/tests/uat/cost-alerts.spec.ts: alert 0.60 set via Set an alert; warning toast, Cost alert banner and account-menu row appeared showing the full-hour amount; Mailpit received exactly one email and none in the next 45 s."

### 3. Deferred follow-up: dismissal feedback and persistence. Dismiss one alert and use Dismiss all in the banner, then restart with ./scripts/Start-Uat.ps1 (no -Fresh) and reload
expected: Each dismissal shows a success toast naming the server and amount (or the count); nothing auto-dismisses; undismissed alerts survive the API restart and dismissed ones stay gone; no second email or alert for the same amount
result: pass
source: automated
evidence: "sources/ReactApp/tests/uat/cost-alerts.spec.ts (fresh phase, restart without -Fresh, then UAT_PHASE=restarted): single dismissal and Dismiss all each showed a success toast naming server and amount or count; nothing auto-dismissed over 75 s and a reload; undismissed alerts survived the API restart, dismissed stayed gone; no new email after the restart."

### 4. Deferred follow-up: redesigned UI. Look at the workspace banner, the account menu alert rows and the cost alerts page (/next/command/servers/cost-alerts) at desktop width and a narrow window, in English and German
expected: The banner is one quiet panel with the cost-stop button and Dismiss all in one footer; menu rows match the server rows; the settings page reads as raised cards with channel status badges. The user judges whether it no longer looks ugly
result: skipped
reason: "Deferred follow-up: visual design judgment is a human call, collected for the end-of-milestone pass. Screenshots (en/de, desktop and 390 px: banner, menu rows, cost alerts page) are produced by sources/ReactApp/tests/uat/cost-design-shots.spec.ts."

### 5. Deferred follow-up (optional, skippable): Discord DM. Needs a bot token in the git-ignored sources/Api/appsettings.secret.json and a linked Discord account
expected: The alert also arrives as one Discord DM
result: skipped
reason: "Deferred follow-up: real-environment test (needs a Discord bot token and a linked account), run at the very end of the milestone."

## Summary

total: 5
passed: 3
issues: 0
pending: 0
skipped: 2
blocked: 0
