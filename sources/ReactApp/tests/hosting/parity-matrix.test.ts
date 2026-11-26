import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { workspaceTabIds } from "@/features/hosting/workspaceSections";
import { hostingService } from "@/features/hosting/services";

/**
 * Completeness and vocabulary guard for the server capability parity matrix (FND-07, D-08, D-12).
 * The matrix lives under .planning and is read from the repository root, so it must be tracked in git and
 * present in the CI source transfer. A missing file fails every case instead of passing vacuously.
 */

const matrixRelativePath =
  ".planning/milestones/v2.1-phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md";
// From tests/hosting the repository root is four levels up. A plain path join is used instead of
// new URL(..., import.meta.url) because Vite treats that pattern as an asset import and rejects the root.
const matrixPath = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../../../..",
  matrixRelativePath,
);
const requiredColumns = [
  "ID",
  "Capability",
  "Today",
  "Status",
  "New location",
  "Phase",
  "Evidence",
] as const;
const allowedStatuses = ["pending", "kept", "relocated", "removed"] as const;
const requiredActionIds = [
  "start",
  "stop",
  "terminate",
  "restart",
  "install",
  "update",
  "collect-logs",
] as const;
const notApplicableValues = ["", "—", "-"];
/** The `hostingService` methods the guided create page calls (Phase 64, D-08). */
const createServiceMethods = [
  "capabilities",
  "create",
  "creationModPacks",
  "terrains",
  "scenarios",
  "creationCostOwners",
  "profileOptions",
  "estimateStorage",
] as const;

/** The capability rows Phase 66 added; each must stay `kept` in phase 66 with Phase 66 test evidence (FND-07). */
const phase66CapabilityIds = [
  "CST-accrued",
  "CST-alert-banner",
  "CST-alert-menu",
  "CST-alert-settings",
] as const;
/** The existing rows Phase 66 touched, with the owner phase each must keep. */
const phase66TouchedRows = [
  { id: "FLT-profile-menu-notifications", phase: "62" },
  { id: "FACT-estimated-cost", phase: "63" },
  { id: "FACT-cost-owner", phase: "63" },
  { id: "FACT-possible-cost-warning", phase: "63" },
] as const;

/** The capability rows Phase 67 added; each must stay `kept` in phase 67 with Phase 67 test evidence (FND-07). */
const phase67CapabilityIds = [
  "MET-tiles",
  "MET-history",
  "SVC-serverMetrics",
  "SVC-serverMetricsHistory",
] as const;
/** The existing rows Phase 67 touched, with the owner phase each must keep. */
const phase67TouchedRows = [{ id: "TAB-overview", phase: "63" }] as const;

/** The capability rows Phase 68 added; each must stay `kept` in phase 68 with Phase 68 test evidence (FND-07). */
const phase68CapabilityIds = [
  "AUT-settings-panel",
  "AUT-warning-notice",
  "AUT-activity-trigger",
  "AUT-overview-fact",
] as const;
/** The existing rows Phase 68 touched, with the owner phase each must keep. */
const phase68TouchedRows = [
  { id: "TAB-settings", phase: "63" },
  { id: "FLT-profile-menu-notifications", phase: "62" },
] as const;

/** The capability rows Phase 69 owns; each must stay `kept` in phase 69 with Phase 69 test evidence (D-19, FND-07). */
const phase69CapabilityIds = [
  "SVC-profiles",
  "SVC-profileCatalog",
  "SVC-profile",
  "SVC-profileAccess",
  "SVC-createProfile",
  "SVC-createProfileRevision",
  "SVC-profileUserCandidates",
  "SVC-profileRoleCandidates",
  "SVC-updateProfileAccessMatrix",
  "SVC-archiveProfile",
  "SVC-changeProfileUserAccess",
  "SVC-changeProfileRoleAccess",
  "ADJ-profiles-library",
  "ADJ-profiles-create",
  "ADJ-profiles-workspace",
  "ADJ-profiles-revisions",
  "ADJ-profiles-access",
  "ADJ-profiles-archive",
  "ADJ-finance-owners",
  "ADJ-finance-account",
  "ADJ-finance-correction",
  "ADJ-finance-settings",
  "ADJ-finance-ledger",
  "ADJ-finance-account-picker",
  "ADJ-route-manifest",
  "ADJ-admin-content-pickers",
] as const;
/** The Phase 69 rows that have no screen caller and must say so in their evidence. */
const phase69ServiceOnlyIds = [
  "SVC-changeProfileUserAccess",
  "SVC-changeProfileRoleAccess",
] as const;

/** The capability rows Phase 71 owns; each must stay `kept` in phase 71 with Phase 71 test evidence (FND-07). */
const phase71CapabilityIds = [
  "SVC-serverAccess",
  "SVC-serverUserCandidates",
  "SVC-serverRoleCandidates",
  "SVC-changeServerUserAccess",
  "SVC-changeServerRoleAccess",
  "SVC-changeServerCostOwner",
  "SVC-serverCostOwnerOptions",
  "ADJ-server-access-panel",
  "ADJ-server-cost-owner-panel",
] as const;

interface MatrixRow {
  id: string;
  capability: string;
  today: string;
  status: string;
  newLocation: string;
  phase: string;
  evidence: string;
  cells: string[];
}

interface Matrix {
  header: string[];
  rows: MatrixRow[];
}

function splitCells(line: string): string[] {
  return line
    .trim()
    .replace(/^\|/, "")
    .replace(/\|$/, "")
    .split("|")
    .map((cell) => cell.trim());
}

function isSeparator(cells: string[]): boolean {
  return cells.length > 0 && cells.every((cell) => /^:?-{3,}:?$/.test(cell) || /^-+$/.test(cell));
}

function loadMatrix(): Matrix {
  if (!existsSync(matrixPath)) {
    throw new Error(
      `Parity matrix not found at ${matrixRelativePath} (decision D-08). ` +
        "The file must be tracked in git and present in the CI source transfer; " +
        "a missing matrix must fail the job instead of passing vacuously.",
    );
  }
  const tableLines = readFileSync(matrixPath, "utf8")
    .split(/\r?\n/)
    .filter((line) => line.trimStart().startsWith("|"));
  const parsed = tableLines.map(splitCells).filter((cells) => !isSeparator(cells));
  const [header, ...body] = parsed;
  if (!header) throw new Error(`Parity matrix ${matrixRelativePath} contains no table.`);
  const rows = body.map((cells) => ({
    id: cells[0] ?? "",
    capability: cells[1] ?? "",
    today: cells[2] ?? "",
    status: cells[3] ?? "",
    newLocation: cells[4] ?? "",
    phase: cells[5] ?? "",
    evidence: cells[6] ?? "",
    cells,
  }));
  return { header, rows };
}

/**
 * Finds what is wrong with the Phase 66 rows of a matrix.
 *
 * Every CST- capability row must exist, have the status `kept`, belong to phase 66 and carry a "Phase 66:" evidence
 * sentence with a tests/ path. Every touched older row must keep its owner phase and gain a "Phase 66:" sentence.
 *
 * @param rows The parsed matrix rows.
 * @returns One message per problem, empty when the rows are correct.
 */
function phase66Offenders(rows: readonly MatrixRow[]): string[] {
  const offenders: string[] = [];
  for (const id of phase66CapabilityIds) {
    const row = rows.find((candidate) => candidate.id === id);
    if (!row) {
      offenders.push(`${id}: row is missing`);
      continue;
    }
    if (row.status !== "kept") offenders.push(`${id}: status is "${row.status}", not "kept"`);
    if (row.phase !== "66") offenders.push(`${id}: phase is "${row.phase}", not "66"`);
    if (!/Phase 66:/.test(row.evidence) || !/tests\//.test(row.evidence)) {
      offenders.push(`${id}: evidence lacks a "Phase 66:" sentence and a tests/ path`);
    }
  }
  for (const { id, phase } of phase66TouchedRows) {
    const row = rows.find((candidate) => candidate.id === id);
    if (!row) {
      offenders.push(`${id}: row is missing`);
      continue;
    }
    if (row.phase !== phase) offenders.push(`${id}: phase changed to "${row.phase}"`);
    if (!/Phase 66:/.test(row.evidence))
      offenders.push(`${id}: evidence lacks a "Phase 66:" sentence`);
  }
  return offenders;
}

/**
 * Finds what is wrong with the Phase 67 rows of a matrix.
 *
 * Every Phase 67 capability row must exist, have the status `kept`, belong to phase 67 and carry a "Phase 67:"
 * evidence sentence with a tests/ path. Every touched older row must keep its owner phase and gain a "Phase 67:"
 * sentence.
 *
 * @param rows The parsed matrix rows.
 * @returns One message per problem, empty when the rows are correct.
 */
function phase67Offenders(rows: readonly MatrixRow[]): string[] {
  const offenders: string[] = [];
  for (const id of phase67CapabilityIds) {
    const row = rows.find((candidate) => candidate.id === id);
    if (!row) {
      offenders.push(`${id}: row is missing`);
      continue;
    }
    if (row.status !== "kept") offenders.push(`${id}: status is "${row.status}", not "kept"`);
    if (row.phase !== "67") offenders.push(`${id}: phase is "${row.phase}", not "67"`);
    if (!/Phase 67:/.test(row.evidence) || !/tests\//.test(row.evidence)) {
      offenders.push(`${id}: evidence lacks a "Phase 67:" sentence and a tests/ path`);
    }
  }
  for (const { id, phase } of phase67TouchedRows) {
    const row = rows.find((candidate) => candidate.id === id);
    if (!row) {
      offenders.push(`${id}: row is missing`);
      continue;
    }
    if (row.phase !== phase) offenders.push(`${id}: phase changed to "${row.phase}"`);
    if (!/Phase 67:/.test(row.evidence))
      offenders.push(`${id}: evidence lacks a "Phase 67:" sentence`);
  }
  return offenders;
}

/**
 * Finds what is wrong with the Phase 68 rows of a matrix.
 *
 * Every AUT- capability row must exist, have the status `kept` (the guard has no `New` status, so a new capability
 * is recorded as kept with a Phase 68 sentence, like Phase 66 and 67), belong to phase 68 and carry a "Phase 68:"
 * evidence sentence with a tests/ path. Every touched older row must keep its owner phase and gain a "Phase 68:"
 * sentence.
 *
 * @param rows The parsed matrix rows.
 * @returns One message per problem, empty when the rows are correct.
 */
function phase68Offenders(rows: readonly MatrixRow[]): string[] {
  const offenders: string[] = [];
  for (const id of phase68CapabilityIds) {
    const row = rows.find((candidate) => candidate.id === id);
    if (!row) {
      offenders.push(`${id}: row is missing`);
      continue;
    }
    if (row.status !== "kept") offenders.push(`${id}: status is "${row.status}", not "kept"`);
    if (row.phase !== "68") offenders.push(`${id}: phase is "${row.phase}", not "68"`);
    if (!/Phase 68:/.test(row.evidence) || !/tests\//.test(row.evidence)) {
      offenders.push(`${id}: evidence lacks a "Phase 68:" sentence and a tests/ path`);
    }
  }
  for (const { id, phase } of phase68TouchedRows) {
    const row = rows.find((candidate) => candidate.id === id);
    if (!row) {
      offenders.push(`${id}: row is missing`);
      continue;
    }
    if (row.phase !== phase) offenders.push(`${id}: phase changed to "${row.phase}"`);
    if (!/Phase 68:/.test(row.evidence))
      offenders.push(`${id}: evidence lacks a "Phase 68:" sentence`);
  }
  return offenders;
}

/**
 * Finds what is wrong with the Phase 69 rows of a matrix.
 *
 * Every Phase 69 capability row must exist, have the status `kept` (no capability is removed, D-19), belong to
 * phase 69 and carry a "Phase 69:" evidence sentence with a tests/ path. The two service-only profile access rows
 * must say "service and tests only". No row of the matrix may have owner phase 69 outside the list.
 *
 * @param rows The parsed matrix rows.
 * @returns One message per problem, empty when the rows are correct.
 */
function phase69Offenders(rows: readonly MatrixRow[]): string[] {
  const offenders: string[] = [];
  for (const id of phase69CapabilityIds) {
    const row = rows.find((candidate) => candidate.id === id);
    if (!row) {
      offenders.push(`${id}: row is missing`);
      continue;
    }
    if (row.status !== "kept") offenders.push(`${id}: status is "${row.status}", not "kept"`);
    if (row.phase !== "69") offenders.push(`${id}: phase is "${row.phase}", not "69"`);
    if (!/Phase 69:/.test(row.evidence) || !/tests\//.test(row.evidence)) {
      offenders.push(`${id}: evidence lacks a "Phase 69:" sentence and a tests/ path`);
    }
  }
  for (const id of phase69ServiceOnlyIds) {
    const row = rows.find((candidate) => candidate.id === id);
    if (row && !/service and tests only/.test(row.evidence)) {
      offenders.push(`${id}: evidence lacks "service and tests only"`);
    }
  }
  const known = new Set<string>(phase69CapabilityIds);
  for (const row of rows) {
    if (row.phase === "69" && !known.has(row.id)) {
      offenders.push(`${row.id}: owned by phase 69 but missing from phase69CapabilityIds`);
    }
  }
  return offenders;
}

/**
 * Finds what is wrong with the Phase 71 rows of a matrix.
 *
 * Every Phase 71 capability row must exist, have the status `kept` (no capability is removed), belong to phase 71
 * and carry a "Phase 71:" evidence sentence with a tests/ path. No row of the matrix may have owner phase 71 outside
 * the list.
 *
 * @param rows The parsed matrix rows.
 * @returns One message per problem, empty when the rows are correct.
 */
function phase71Offenders(rows: readonly MatrixRow[]): string[] {
  const offenders: string[] = [];
  for (const id of phase71CapabilityIds) {
    const row = rows.find((candidate) => candidate.id === id);
    if (!row) {
      offenders.push(`${id}: row is missing`);
      continue;
    }
    if (row.status !== "kept") offenders.push(`${id}: status is "${row.status}", not "kept"`);
    if (row.phase !== "71") offenders.push(`${id}: phase is "${row.phase}", not "71"`);
    if (!/Phase 71:/.test(row.evidence) || !/tests\//.test(row.evidence)) {
      offenders.push(`${id}: evidence lacks a "Phase 71:" sentence and a tests/ path`);
    }
  }
  const known = new Set<string>(phase71CapabilityIds);
  for (const row of rows) {
    if (row.phase === "71" && !known.has(row.id)) {
      offenders.push(`${row.id}: owned by phase 71 but missing from phase71CapabilityIds`);
    }
  }
  return offenders;
}

/**
 * Finds the rows that are still `pending` after the last UI phase.
 *
 * @param rows The parsed matrix rows.
 * @returns The id of every row with the status `pending`.
 */
function pendingRows(rows: readonly MatrixRow[]): string[] {
  return rows.filter((row) => row.status === "pending").map((row) => row.id);
}

describe("parity matrix", () => {
  it("is found at the repository-root-relative D-08 path", () => {
    expect(() => loadMatrix()).not.toThrow();
  });

  it("has the seven required columns in order", () => {
    expect(loadMatrix().header).toEqual([...requiredColumns]);
  });

  it("gives every row exactly seven cells and no empty cell", () => {
    const offenders = loadMatrix()
      .rows.filter(
        (row) =>
          row.cells.length !== requiredColumns.length || row.cells.some((cell) => cell === ""),
      )
      .map((row) => row.id || "(row without id)");
    expect(offenders, `rows with a missing or empty cell: ${offenders.join(", ")}`).toEqual([]);
  });

  it("uses unique row ids", () => {
    const seen = new Set<string>();
    const duplicates: string[] = [];
    for (const row of loadMatrix().rows) {
      if (seen.has(row.id)) duplicates.push(row.id);
      seen.add(row.id);
    }
    expect(duplicates, `duplicate row ids: ${duplicates.join(", ")}`).toEqual([]);
  });

  it("uses only the allowed status vocabulary", () => {
    const offenders = loadMatrix()
      .rows.filter((row) => !(allowedStatuses as readonly string[]).includes(row.status))
      .map((row) => `${row.id}: "${row.status}"`);
    expect(offenders, `rows with an empty or unknown status: ${offenders.join(", ")}`).toEqual([]);
  });

  it.each([...workspaceTabIds])("has a row for workspace tab %s", (tabId) => {
    const ids = loadMatrix().rows.map((row) => row.id);
    expect(ids, `missing parity matrix row TAB-${tabId}`).toContain(`TAB-${tabId}`);
  });

  it.each(Object.keys(hostingService))("has a row for hostingService.%s", (method) => {
    const row = loadMatrix().rows.find((candidate) => candidate.id === `SVC-${method}`);
    expect(row, `missing parity matrix row SVC-${method}`).toBeDefined();
    expect(
      row?.today,
      `row SVC-${method} must name hostingService.${method} in the Today cell`,
    ).toContain(`hostingService.${method}`);
  });

  it.each([...requiredActionIds])("has a row for action %s", (actionId) => {
    const ids = loadMatrix().rows.map((row) => row.id);
    expect(ids, `missing parity matrix row ACT-${actionId}`).toContain(`ACT-${actionId}`);
  });

  it("requires a new location for relocated rows", () => {
    const offenders = loadMatrix()
      .rows.filter(
        (row) => row.status === "relocated" && notApplicableValues.includes(row.newLocation),
      )
      .map((row) => row.id);
    expect(offenders, `relocated rows without a new location: ${offenders.join(", ")}`).toEqual([]);
  });

  it("keeps all seventeen create-form rows kept or relocated after the guided create page (D-08)", () => {
    const rows = loadMatrix().rows.filter((row) => row.id.startsWith("CRT-"));
    expect(rows, "the CRT row family must not lose or gain a row").toHaveLength(17);
    const offenders = rows
      .filter((row) => row.status !== "kept" && row.status !== "relocated")
      .map((row) => `${row.id}: "${row.status}"`);
    expect(
      offenders,
      `CRT rows that are neither kept nor relocated: ${offenders.join(", ")}`,
    ).toEqual([]);
  });

  it("keeps every Phase 65 row kept or relocated, with test evidence and no deleted row (D-08)", () => {
    const rows = loadMatrix().rows.filter((row) => row.phase === "65");
    // 4 workspace tabs, 23 SVC rows (22 at plan time plus SVC-modPackContents), 12 CHG rows and 13 FIL rows.
    expect(
      rows.length,
      "Phase 65 owns at least 48 rows; a deleted row lowers this count",
    ).toBeGreaterThanOrEqual(48);
    const offenders = rows
      .filter((row) => row.status !== "kept" && row.status !== "relocated")
      .map((row) => `${row.id}: "${row.status}"`);
    expect(
      offenders,
      `Phase 65 rows that are neither kept nor relocated: ${offenders.join(", ")}`,
    ).toEqual([]);
    const withoutEvidence = rows
      .filter((row) => !/Phase 65:/.test(row.evidence) || !/tests\//.test(row.evidence))
      .map((row) => row.id);
    expect(
      withoutEvidence,
      `Phase 65 rows without a "Phase 65:" evidence sentence and a test file: ${withoutEvidence.join(", ")}`,
    ).toEqual([]);
  });

  it("keeps the four Phase 66 CST rows kept with Phase 66 test evidence and marks the touched rows (FND-07)", () => {
    const offenders = phase66Offenders(loadMatrix().rows);
    expect(offenders, `Phase 66 matrix problems: ${offenders.join("; ")}`).toEqual([]);
  });

  it("reports a Phase 66 row that is missing, not kept or without evidence (live negative sample)", () => {
    const sample = (id: string, status: string, evidence: string): MatrixRow => ({
      id,
      capability: "sample",
      today: "sample",
      status,
      newLocation: "sample",
      phase: "66",
      evidence,
      cells: [],
    });
    const touched = phase66TouchedRows.map(({ id, phase }) => ({
      ...sample(id, "kept", "Phase 66: sample; tests/sample.test.ts"),
      phase,
    }));
    const complete = phase66CapabilityIds.map((id) =>
      sample(id, "kept", "Phase 66: sample; tests/sample.test.ts"),
    );
    expect(phase66Offenders([...complete, ...touched])).toEqual([]);

    const withoutEvidence = complete.map((row) =>
      row.id === "CST-accrued" ? { ...row, evidence: "Phase 66: sample" } : row,
    );
    expect(phase66Offenders([...withoutEvidence, ...touched])).toEqual([
      'CST-accrued: evidence lacks a "Phase 66:" sentence and a tests/ path',
    ]);

    const notKept = complete.map((row) =>
      row.id === "CST-alert-menu" ? { ...row, status: "pending" } : row,
    );
    expect(phase66Offenders([...notKept, ...touched])).toEqual([
      'CST-alert-menu: status is "pending", not "kept"',
    ]);

    expect(phase66Offenders([...complete.slice(1), ...touched])).toEqual([
      "CST-accrued: row is missing",
    ]);

    const unmarked = touched.map((row) =>
      row.id === "FACT-cost-owner" ? { ...row, evidence: "Phase 63: sample" } : row,
    );
    expect(phase66Offenders([...complete, ...unmarked])).toEqual([
      'FACT-cost-owner: evidence lacks a "Phase 66:" sentence',
    ]);
  });

  it("keeps the Phase 67 metrics rows kept with Phase 67 test evidence and marks the touched row (FND-07)", () => {
    const offenders = phase67Offenders(loadMatrix().rows);
    expect(offenders, `Phase 67 matrix problems: ${offenders.join("; ")}`).toEqual([]);
  });

  it("reports a Phase 67 row that is missing, not kept or without evidence (live negative sample)", () => {
    const sample = (id: string, status: string, evidence: string): MatrixRow => ({
      id,
      capability: "sample",
      today: "sample",
      status,
      newLocation: "sample",
      phase: "67",
      evidence,
      cells: [],
    });
    const touched = phase67TouchedRows.map(({ id, phase }) => ({
      ...sample(id, "kept", "Phase 67: sample; tests/sample.test.ts"),
      phase,
    }));
    const complete = phase67CapabilityIds.map((id) =>
      sample(id, "kept", "Phase 67: sample; tests/sample.test.ts"),
    );
    expect(phase67Offenders([...complete, ...touched])).toEqual([]);

    const withoutEvidence = complete.map((row) =>
      row.id === "MET-tiles" ? { ...row, evidence: "Phase 67: sample" } : row,
    );
    expect(phase67Offenders([...withoutEvidence, ...touched])).toEqual([
      'MET-tiles: evidence lacks a "Phase 67:" sentence and a tests/ path',
    ]);

    const notKept = complete.map((row) =>
      row.id === "MET-history" ? { ...row, status: "pending" } : row,
    );
    expect(phase67Offenders([...notKept, ...touched])).toEqual([
      'MET-history: status is "pending", not "kept"',
    ]);

    expect(phase67Offenders([...complete.slice(1), ...touched])).toEqual([
      "MET-tiles: row is missing",
    ]);

    const unmarked = touched.map((row) =>
      row.id === "TAB-overview" ? { ...row, evidence: "Phase 63: sample" } : row,
    );
    expect(phase67Offenders([...complete, ...unmarked])).toEqual([
      'TAB-overview: evidence lacks a "Phase 67:" sentence',
    ]);
  });

  it("keeps the four Phase 68 AUT rows kept with Phase 68 test evidence and marks the touched rows (FND-07)", () => {
    const offenders = phase68Offenders(loadMatrix().rows);
    expect(offenders, `Phase 68 matrix problems: ${offenders.join("; ")}`).toEqual([]);
  });

  it("reports a Phase 68 row that is missing, not kept or without evidence (live negative sample)", () => {
    const sample = (id: string, status: string, evidence: string): MatrixRow => ({
      id,
      capability: "sample",
      today: "sample",
      status,
      newLocation: "sample",
      phase: "68",
      evidence,
      cells: [],
    });
    const touched = phase68TouchedRows.map(({ id, phase }) => ({
      ...sample(id, "kept", "Phase 68: sample; tests/sample.test.ts"),
      phase,
    }));
    const complete = phase68CapabilityIds.map((id) =>
      sample(id, "kept", "Phase 68: sample; tests/sample.test.ts"),
    );
    expect(phase68Offenders([...complete, ...touched])).toEqual([]);

    const withoutEvidence = complete.map((row) =>
      row.id === "AUT-settings-panel" ? { ...row, evidence: "Phase 68: sample" } : row,
    );
    expect(phase68Offenders([...withoutEvidence, ...touched])).toEqual([
      'AUT-settings-panel: evidence lacks a "Phase 68:" sentence and a tests/ path',
    ]);

    const notKept = complete.map((row) =>
      row.id === "AUT-warning-notice" ? { ...row, status: "pending" } : row,
    );
    expect(phase68Offenders([...notKept, ...touched])).toEqual([
      'AUT-warning-notice: status is "pending", not "kept"',
    ]);

    expect(phase68Offenders([...complete.slice(1), ...touched])).toEqual([
      "AUT-settings-panel: row is missing",
    ]);

    const unmarked = touched.map((row) =>
      row.id === "TAB-settings" ? { ...row, evidence: "Phase 63: sample" } : row,
    );
    expect(phase68Offenders([...complete, ...unmarked])).toEqual([
      'TAB-settings: evidence lacks a "Phase 68:" sentence',
    ]);

    const movedOwner = touched.map((row) =>
      row.id === "FLT-profile-menu-notifications" ? { ...row, phase: "68" } : row,
    );
    expect(phase68Offenders([...complete, ...movedOwner])).toEqual([
      'FLT-profile-menu-notifications: phase changed to "68"',
    ]);
  });

  it("keeps the 26 Phase 69 rows kept with Phase 69 test evidence (D-19, FND-07)", () => {
    expect(phase69CapabilityIds).toHaveLength(26);
    const offenders = phase69Offenders(loadMatrix().rows);
    expect(offenders, `Phase 69 matrix problems: ${offenders.join("; ")}`).toEqual([]);
  });

  it("reports a Phase 69 row that is missing, not kept, without evidence or unlisted (live negative sample)", () => {
    const sample = (id: string, status: string, evidence: string): MatrixRow => ({
      id,
      capability: "sample",
      today: "sample",
      status,
      newLocation: "sample",
      phase: "69",
      evidence,
      cells: [],
    });
    const complete = phase69CapabilityIds.map((id) =>
      sample(
        id,
        "kept",
        phase69ServiceOnlyIds.some((serviceOnly) => serviceOnly === id)
          ? "Phase 69: service and tests only; tests/sample.test.ts"
          : "Phase 69: sample; tests/sample.test.ts",
      ),
    );
    expect(phase69Offenders(complete)).toEqual([]);

    const withoutEvidence = complete.map((row) =>
      row.id === "ADJ-route-manifest" ? { ...row, evidence: "Phase 69: sample" } : row,
    );
    expect(phase69Offenders(withoutEvidence)).toEqual([
      'ADJ-route-manifest: evidence lacks a "Phase 69:" sentence and a tests/ path',
    ]);

    const pending = complete.map((row) =>
      row.id === "SVC-profiles" ? { ...row, status: "pending" } : row,
    );
    expect(phase69Offenders(pending)).toEqual(['SVC-profiles: status is "pending", not "kept"']);

    const removed = complete.map((row) =>
      row.id === "ADJ-finance-ledger" ? { ...row, status: "removed" } : row,
    );
    expect(phase69Offenders(removed)).toEqual([
      'ADJ-finance-ledger: status is "removed", not "kept"',
    ]);

    expect(phase69Offenders(complete.slice(1))).toEqual(["SVC-profiles: row is missing"]);

    const serviceOnly = complete.map((row) =>
      row.id === "SVC-changeProfileUserAccess"
        ? { ...row, evidence: "Phase 69: sample; tests/sample.test.ts" }
        : row,
    );
    expect(phase69Offenders(serviceOnly)).toEqual([
      'SVC-changeProfileUserAccess: evidence lacks "service and tests only"',
    ]);

    expect(
      phase69Offenders([...complete, sample("ADJ-unlisted", "kept", "Phase 69: sample; tests/x")]),
    ).toEqual(["ADJ-unlisted: owned by phase 69 but missing from phase69CapabilityIds"]);
  });

  it("keeps the Phase 71 rows kept with Phase 71 test evidence (FND-07)", () => {
    expect(phase71CapabilityIds.length).toBeGreaterThan(0);
    const offenders = phase71Offenders(loadMatrix().rows);
    expect(offenders, `Phase 71 matrix problems: ${offenders.join("; ")}`).toEqual([]);
  });

  it("reports a Phase 71 row that is missing, not kept, without evidence or unlisted (live negative sample)", () => {
    const sample = (id: string, status: string, evidence: string): MatrixRow => ({
      id,
      capability: "sample",
      today: "sample",
      status,
      newLocation: "sample",
      phase: "71",
      evidence,
      cells: [],
    });
    const complete = phase71CapabilityIds.map((id) =>
      sample(id, "kept", "Phase 71: sample; tests/sample.test.ts"),
    );
    expect(phase71Offenders(complete)).toEqual([]);

    const withoutEvidence = complete.map((row) =>
      row.id === "SVC-serverAccess" ? { ...row, evidence: "Phase 71: sample" } : row,
    );
    expect(phase71Offenders(withoutEvidence)).toEqual([
      'SVC-serverAccess: evidence lacks a "Phase 71:" sentence and a tests/ path',
    ]);

    const pending = complete.map((row) =>
      row.id === "SVC-serverAccess" ? { ...row, status: "pending" } : row,
    );
    expect(phase71Offenders(pending)).toEqual([
      'SVC-serverAccess: status is "pending", not "kept"',
    ]);

    expect(phase71Offenders(complete.slice(1))).toEqual(["SVC-serverAccess: row is missing"]);

    expect(
      phase71Offenders([...complete, sample("ADJ-unlisted", "kept", "Phase 71: sample; tests/x")]),
    ).toEqual(["ADJ-unlisted: owned by phase 71 but missing from phase71CapabilityIds"]);
  });

  it("leaves no row pending after Phase 69 (D-19)", () => {
    const pending = pendingRows(loadMatrix().rows);
    expect(pending, `rows that are still pending: ${pending.join(", ")}`).toEqual([]);
  });

  it("reports a pending row by id (live negative sample)", () => {
    const row = (id: string, status: string): MatrixRow => ({
      id,
      capability: "sample",
      today: "sample",
      status,
      newLocation: "sample",
      phase: "69",
      evidence: "sample",
      cells: [],
    });

    expect(pendingRows([row("A-1", "kept"), row("A-2", "pending")])).toEqual(["A-2"]);
  });

  it.each(createServiceMethods)(
    "keeps the create-related service row SVC-%s because the guided create page still calls it (D-08)",
    (method) => {
      const row = loadMatrix().rows.find((candidate) => candidate.id === `SVC-${method}`);
      expect(row, `missing parity matrix row SVC-${method}`).toBeDefined();
      expect(row?.status, `SVC-${method} must stay kept`).toBe("kept");
    },
  );

  it("requires a written reason and a recorded sign-off for removed rows", () => {
    const offenders = loadMatrix()
      .rows.filter((row) => {
        if (row.status !== "removed") return false;
        const reason = row.evidence.replace(/sign-off/gi, "").trim();
        return !/sign-off/i.test(row.evidence) || notApplicableValues.includes(reason);
      })
      .map((row) => row.id);
    expect(
      offenders,
      `removed rows without a reason and a recorded user sign-off: ${offenders.join(", ")}`,
    ).toEqual([]);
  });
});
