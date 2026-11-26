# Phase 57: Guided Server Creation Journey - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md - this log preserves the alternatives considered.

**Date:** 2026-07-05
**Phase:** 57-Guided Server Creation Journey
**Areas discussed:** Journey shape, Hosting-mode language, Cloud continuation, Blocker feedback

---

## Journey Shape

### Flow Structure

| Option | Description | Selected |
|--------|-------------|----------|
| Single progressive page | Start with game, then reveal hosting mode, owner, runtime/template/options, review, and next action on the same path. | |
| Step-by-step wizard | Separate screens for each decision; clearer sequence, but more navigation/state work. | Yes |
| Mode-first tabs | Keep record/local/cloud as top-level modes; least disruptive, but keeps the current split before users choose the game. | |
| You decide | Agent discretion. | |

**User's choice:** Step-by-step wizard.
**Notes:** The user preferred a wizard structure over the current mode-first page.

### Save Timing

| Option | Description | Selected |
|--------|-------------|----------|
| After basics are complete | Save once game, hosting mode, title, and owner are selected, then continue against that server. | |
| Only at final submit | Keep everything as a draft until the user confirms the whole journey. | Yes |
| Ask before saving | Prompt explicitly before creating the record. | |
| You decide | Agent discretion. | |

**User's choice:** Only after the wizard is fully completed.
**Notes:** The user explicitly said nothing should be saved during wizard steps because it will confuse users.

### Final Submit

| Option | Description | Selected |
|--------|-------------|----------|
| Create and start the next task | Record creates the server; local queues install; cloud starts cloud readiness review. | |
| Create only, then offer next actions | Save the server, then show actions such as install or review cloud setup. | |
| Create and open workspace | Save the server and navigate to the server workspace. | Yes |
| You decide | Agent discretion. | |

**User's choice:** Create and open workspace.
**Notes:** Further setup wizards can be offered from the workspace.

### Wizard Scope

| Option | Description | Selected |
|--------|-------------|----------|
| Identity and ownership only | Game, hosting mode, title/name, billing owner, plus only fields required to create the server record. | |
| Enough for first runnable action | Include local install or cloud review prerequisites before submit, but still save only at the end. | Yes |
| Everything except provider mutation | Include runtime/template/cost/policy review before submit. | |
| You decide | Agent discretion. | |

**User's choice:** Enough for first runnable action.
**Notes:** The wizard should gather enough for the first runnable action while preserving the no-save-before-final-submit rule.

---

## Hosting-Mode Language

### Mode Labels

| Option | Description | Selected |
|--------|-------------|----------|
| Server record / Local server / Cloud server | Plain, short labels; details explain what each does. | Yes |
| Track manually / Host on this machine / Host in the cloud | More action-oriented, but less compact. | |
| Manual setup / Local managed / Cloud managed | Closer to technical terms, but managed may still need explanation. | |
| You decide | Agent discretion. | |

**User's choice:** Server record / Local server / Cloud server.
**Notes:** These labels should replace current Arma/cloud-specific wording in the creation choice.

### Mode Descriptions

| Option | Description | Selected |
|--------|-------------|----------|
| Task-focused one-liners | Short explanations such as create a record now and set it up later. | Yes |
| Operational detail | Mention lifecycle jobs, workers, dry-run review, policy gates, and provider provisioning. | |
| Minimal labels only | No helper copy unless a mode is selected. | |
| You decide | Agent discretion. | |

**User's choice:** Task-focused one-liners.
**Notes:** Initial mode choice should avoid implementation nouns.

### Internal Terms

| Option | Description | Selected |
|--------|-------------|----------|
| Only in detail or error context | Normal flow uses task wording; technical terms appear in details, diagnostics, or exact failures. | Yes |
| Never in the wizard | Fully hide those terms until workspace/admin pages. | |
| Show them as secondary metadata | Visible but de-emphasized for users who want detail. | |
| You decide | Agent discretion. | |

**User's choice:** Only in detail or error context.
**Notes:** Terms like dry-run plan, policy gate, billing assignment, and lifecycle job should not be primary flow language.

### Ownership Label

| Option | Description | Selected |
|--------|-------------|----------|
| Cost owner | Clear to normal users; maps to billing owner internally. | |
| Billing owner | Consistent with current domain language. | Yes |
| Responsible owner | Broader and friendlier, but less explicit about cost. | |
| You decide | Agent discretion. | |

**User's choice:** Billing owner.
**Notes:** Keep current domain label here.

---

## Cloud Continuation

### Continuation Surface

| Option | Description | Selected |
|--------|-------------|----------|
| Server workspace setup wizard | Workspace shows a server-scoped cloud setup wizard/checklist. | |
| Dedicated server cloud setup route | A focused route such as `/command/servers/{id}/cloud/setup`. | |
| Advanced operations page | Reuse the existing advanced cloud panel. | Yes |
| You decide | Agent discretion. | |

**User's choice:** Advanced operations page.
**Notes:** Cloud setup should continue through the existing advanced operations page.

### Discovery From Workspace

| Option | Description | Selected |
|--------|-------------|----------|
| Prominent workspace action | Show a primary Continue cloud setup action that deep-links to advanced operations. | Yes |
| Auto-redirect after creation | Open workspace briefly or with message, then navigate to advanced operations. | |
| Standard navigation only | Rely on existing Advanced operations link. | |
| You decide | Agent discretion. | User selected agent discretion |

**User's choice:** You decide.
**Notes:** Captured under agent discretion as a prominent workspace action to avoid the current discovery problem.

### Carrying Wizard Choices

| Option | Description | Selected |
|--------|-------------|----------|
| Preselect, do not persist extra setup state | Pass server/template/runtime/profile/mod pack/runtime basics as defaults only. | Yes |
| Carry only the server | Advanced operations asks the user to reselect choices. | |
| Persist a setup draft after final submit | Save cloud setup choices with the server for later continuation. | |
| You decide | Agent discretion. | User selected agent discretion |

**User's choice:** You decide.
**Notes:** Captured under agent discretion as preselect without persistent setup draft state.

### Cloud Step Label

| Option | Description | Selected |
|--------|-------------|----------|
| Review cloud setup | Task-oriented and avoids dry-run plan as main label. | Yes |
| Create planning estimate | Describes the estimate/gate, but keeps planning concept visible. | |
| Generate dry-run plan | Technically exact, but product-unfriendly. | |
| You decide | Agent discretion. | User selected agent discretion |

**User's choice:** You decide.
**Notes:** Captured under agent discretion as Review cloud setup.

### Policy State Presentation

| Option | Description | Selected |
|--------|-------------|----------|
| Inline blocker with next action | Show request approval, acknowledge warning, or refresh policy as the next task. | Yes |
| Separate approval page | Send users to a policy-specific screen. | |
| Disable provision only | Show status, but leave discovery to existing policy/admin areas. | |
| You decide | Agent discretion. | User selected agent discretion |

**User's choice:** You decide.
**Notes:** Captured under agent discretion as inline blocker with next action.

---

## Blocker Feedback

### Missing Required Choices

| Option | Description | Selected |
|--------|-------------|----------|
| Inline per step plus summary | Each step marks missing fields and review summarizes blockers. | Yes |
| Review-step only | Let users move through freely, then show all blockers at the end. | |
| Hard stop per step | Users cannot leave a step until required fields are valid. | |
| You decide | Agent discretion. | User selected agent discretion |

**User's choice:** You decide.
**Notes:** Captured under agent discretion as inline per step plus review summary.

### Unavailable Options

| Option | Description | Selected |
|--------|-------------|----------|
| Disabled option/state with fix action | Show unavailable state and next fix such as create owner or refresh metadata. | Yes |
| Hide unavailable options | Simpler UI, but users may not understand why a mode is missing. | |
| Show as generic error banner | Easy to implement, but less actionable. | |
| You decide | Agent discretion. | |

**User's choice:** Disabled option/state with fix action.
**Notes:** Unavailable options should be visible and actionable, not hidden.

### Stale Cloud State

| Option | Description | Selected |
|--------|-------------|----------|
| Explain what changed and regenerate | Show what changed, why it matters, and offer Review cloud setup again. | Yes |
| Silently refresh defaults | Easier path, but hides meaningful changes. | |
| Block with technical stale-plan wording | Precise, but user-hostile. | |
| You decide | Agent discretion. | |

**User's choice:** Explain what changed and regenerate.
**Notes:** The user typed `1l`; this was interpreted as option 1.

### Failed Final Submit

| Option | Description | Selected |
|--------|-------------|----------|
| Stay in the wizard with exact fix targets | Preserve entered choices and let the user correct without restarting. | Yes |
| Open workspace if the server was created, otherwise stay in wizard | Useful for partial success, but must be clear. | |
| Show a generic failure and retry | Simpler, but risks losing user confidence. | |
| You decide | Agent discretion. | |

**User's choice:** Stay in the wizard with exact fix targets.
**Notes:** If final submit fails before server creation, the wizard should not navigate away.

---

## the agent's Discretion

- Use a prominent workspace action for continuing cloud setup after creation.
- Carry wizard choices into advanced operations as preselected navigation defaults only, without persisting extra setup state.
- Label the cloud review action as Review cloud setup.
- Present policy states as inline blockers with next actions.
- Use inline per-step validation plus review-step summary for missing required choices.

## Deferred Ideas

None - discussion stayed within phase scope.
