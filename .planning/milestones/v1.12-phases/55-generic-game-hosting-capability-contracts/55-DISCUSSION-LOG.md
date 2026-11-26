# Phase 55: Generic Game Hosting Capability Contracts - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md - this log preserves the alternatives considered.

**Date:** 2026-07-03
**Phase:** 55-Generic Game Hosting Capability Contracts
**Areas discussed:** Capability metadata shape, Generic cloud entry contract, Arma compatibility boundary, Regression guard strictness

---

## Capability Metadata Shape

| Question | Options Presented | Selected |
|----------|-------------------|----------|
| How should Phase 55 define the supported-game capability contract? | Minimal capability contract; Rich capability contract; Layered contract; Other | Layered contract |
| How should hosting modes be represented in that layered contract? | Explicit enum-like modes; Capability flags; Mode objects; Other | Explicit enum-like modes |
| Which lifecycle actions should the capability expose? | Generic job actions only; UI action groups; Both; Other | Other |
| How much of that action descriptor should Phase 55 require? | Core descriptor only; Core plus inputs; Core plus grouping; Other | Other |

**User's choice:** Layered capability contract; explicit hosting modes; server-specific job actions behind a generic interface; action descriptors with identifier, label, and risk only.
**Notes:** The user rejected an availability field because unsupported actions should simply not be published by the game capability.

---

## Generic Cloud Entry Contract

| Question | Options Presented | Selected |
|----------|-------------------|----------|
| How should Phase 55 introduce generic cloud entry/create contracts? | New generic API routes now; Repository-first generic methods; Add only capability routes in Phase 55; Other | New generic API routes now |
| What should happen to old Arma-specific API routes in Phase 55? | Keep and delegate; Keep untouched; Deprecate hard; Other | Other |
| Should Phase 55 fully remove Arma-specific WebApp repository methods too? | Remove from active repository interface; Keep internal private helpers only; Keep public old methods until all UI phases finish; Other | Remove from active repository interface |
| How should generic dry-run/planning routes handle game specificity in Phase 55? | Route includes game identifier; Payload includes game identifier; Server derives game from server record; Other | Other |

**User's choice:** Add generic API routes now, regenerate the Kiota client, remove old Arma-specific API routes and public repository methods, and derive game type server-side from the server record for server-scoped planning routes.
**Notes:** The user explicitly required regenerated generated-client support instead of handwritten custom client extensions for the new API surface.

---

## Arma Compatibility Boundary

| Question | Options Presented | Selected |
|----------|-------------------|----------|
| Since old Arma-specific routes/methods can be removed, what compatibility must still be preserved? | Behavioral compatibility only; Data compatibility too; Minimal compatibility; Other | Other |
| What should Arma 3 become after the clean cut? | First generic capability implementation; Internal adapter only; Both; Other | First generic capability implementation |
| Should Phase 55 include database/entity cleanup, or only contract/API/WebApp cleanup? | Contract/API/WebApp only; Include obvious entity cleanup; Clean cut now; Other | Clean cut now |
| What should plans do if they find an Arma-specific type deeply tied to runtime profiles/templates outside Phase 55 scope? | Transform if blocking and defer otherwise; Transform aggressively wherever touched; Defer all to Phase 56; Other | Transform aggressively wherever touched |

**User's choice:** No legacy compatibility. Arma 3 becomes the first generic capability implementation, and Arma-specific data may be removed or transformed aggressively because there is no deployment migration cost.
**Notes:** The user stated a clean cut is the best option.

---

## Regression Guard Strictness

| Question | Options Presented | Selected |
|----------|-------------------|----------|
| How strict should Phase 55 tests be about Arma-specific names after the clean cut? | Strict active-source ban; Boundary-focused ban; Contract-shape tests only; Other | the agent decided strict active-source ban |
| What should the tests require after client regeneration? | Generated client proves new routes; Source scan only; Both; Other | the agent decided both |
| Should tests allow generic code to branch on the Arma 3 game identifier? | No branching in generic WebApp/API contracts; Allow API service branching only; Allow temporary branching with TODOs; Other | No branching in generic WebApp/API contracts |
| Should tests enforce removal of Arma-specific persisted/DTO type names in Phase 55, or only block active public contract usage? | Enforce public contract cleanup; Enforce aggressive cleanup; Planner discretion; Other | Enforce aggressive cleanup |

**User's choice:** The user delegated the first two guard choices to the agent, then selected no Arma branching in generic contracts and aggressive cleanup enforcement.
**Notes:** Exceptions are limited to genuine game implementation internals and generic capability metadata values.

---

## the agent's Discretion

- Chose strict active-source regression guards with explicit implementation-internal exceptions.
- Chose both generated-client presence checks and source scans.

## Deferred Ideas

None.
