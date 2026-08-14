import type {
  GameConfigurationEntry,
  HostingFile,
  LifecycleJob,
  ServerAction,
} from "@/features/hosting/types";

export function parentServerPath(path: string): string {
  const normalized = path.replaceAll("\\", "/").replace(/^\/+|\/+$/g, "");
  const index = normalized.lastIndexOf("/");
  return index < 0 ? "" : normalized.slice(0, index);
}

export function combineServerPath(directory: string, name: string): string {
  const left = directory.replaceAll("\\", "/").replace(/^\/+|\/+$/g, "");
  const right = name.replaceAll("\\", "/").replace(/^\/+|\/+$/g, "");
  return left ? `${left}/${right}` : right;
}

export function pathBreadcrumbs(path: string): { label: string; path: string }[] {
  const segments = path.replaceAll("\\", "/").split("/").filter(Boolean);
  let current = "";
  return segments.map((segment) => {
    current = combineServerPath(current, segment);
    return { label: segment, path: current };
  });
}

export function sortHostingFiles(
  files: HostingFile[],
  column: "name" | "modified" | "size",
  descending: boolean,
): HostingFile[] {
  return [...files].sort((left, right) => {
    if (left.isDirectory !== right.isDirectory) return left.isDirectory ? -1 : 1;
    let comparison: number;
    if (column === "modified")
      comparison = (left.modifiedAt ?? "").localeCompare(right.modifiedAt ?? "");
    else if (column === "size") comparison = (left.size ?? -1) - (right.size ?? -1);
    else comparison = left.name.localeCompare(right.name, undefined, { sensitivity: "base" });
    return descending ? -comparison : comparison;
  });
}

export function configurationInputValue(entry: GameConfigurationEntry): string {
  if (entry.isRedacted) return "";
  if (entry.value === undefined || entry.value === null) return "";
  if (Array.isArray(entry.value))
    return entry.value.filter((item) => typeof item === "string").join("\n");
  return typeof entry.value === "string" ? entry.value : String(entry.value);
}

export function parseConfigurationEntryValue(entry: GameConfigurationEntry, raw: string): unknown {
  const kind = entry.kind.toLowerCase();
  if (kind === "boolean") return raw === "true";
  if (kind === "integer") {
    const value = Number(raw);
    if (!Number.isInteger(value)) throw new Error(`${entry.displayName} must be a whole number.`);
    return value;
  }
  if (kind === "number" || kind === "decimal") {
    const value = Number(raw);
    if (!Number.isFinite(value)) throw new Error(`${entry.displayName} must be a number.`);
    return value;
  }
  if (kind === "stringarray")
    return raw
      .split(/\r?\n/)
      .map((value) => value.trim())
      .filter(Boolean);
  return raw;
}

export function validateConfigurationEntry(
  entry: GameConfigurationEntry,
  raw: string,
): string | undefined {
  if (entry.required && raw.trim().length === 0 && !entry.isRedacted)
    return `${entry.displayName} is required.`;
  if (!raw.trim() && !entry.required) return undefined;
  try {
    const value = parseConfigurationEntryValue(entry, raw);
    if (typeof value === "number") {
      if (entry.minimum !== null && value < entry.minimum)
        return `${entry.displayName} must be at least ${entry.minimum}.`;
      if (entry.maximum !== null && value > entry.maximum)
        return `${entry.displayName} must be at most ${entry.maximum}.`;
    }
    if (
      entry.allowedValues.length > 0 &&
      typeof value === "string" &&
      !entry.allowedValues.includes(value)
    ) {
      return `${entry.displayName} has an unsupported value.`;
    }
  } catch (error) {
    return error instanceof Error ? error.message : `${entry.displayName} is invalid.`;
  }
  return undefined;
}

export function primaryServerAction(
  actions: ServerAction[],
  possibleCost: boolean,
): ServerAction | undefined {
  const candidates = actions.filter(
    (action) => action.id !== "collect-logs" && (action.enabled || action.pending),
  );
  if (possibleCost) {
    const costStop = candidates.find(
      (action) => action.dangerous && action.costImpact.toLowerCase().includes("stopscost"),
    );
    if (costStop) return costStop;
  }
  return (
    candidates.find((action) => action.id === "start") ??
    candidates.find((action) => action.id === "stop") ??
    candidates.sort((left, right) => left.order - right.order)[0]
  );
}

export interface ProtocolEntry {
  job: LifecycleJob;
  command: LifecycleJob["commands"][number];
}

export function protocolEntries(jobs: LifecycleJob[]): ProtocolEntry[] {
  return jobs
    .flatMap((job) => job.commands.map((command) => ({ job, command })))
    .sort((left, right) => {
      const leftTime =
        left.command.timeStampCompleted ??
        left.command.timeStampStarted ??
        left.command.timeStampCreated;
      const rightTime =
        right.command.timeStampCompleted ??
        right.command.timeStampStarted ??
        right.command.timeStampCreated;
      return rightTime.localeCompare(leftTime) || right.command.sequence - left.command.sequence;
    });
}

export function buildCommandProtocol(entry: ProtocolEntry): string {
  const { job, command } = entry;
  return [
    "UTF worker command protocol",
    `Job: ${job.primaryKey}`,
    `Command: ${command.primaryKey}`,
    `Sequence: ${command.sequence}`,
    `Action: ${command.actionId}`,
    `Stage: ${command.stage}`,
    `Status: ${command.status}`,
    `Created: ${command.timeStampCreated}`,
    `Started: ${command.timeStampStarted ?? "-"}`,
    `Completed: ${command.timeStampCompleted ?? "-"}`,
    `Failure code: ${command.failureCode ?? "-"}`,
    `Failure: ${command.failureReason ?? "-"}`,
    "",
    command.diagnosticOutput ?? "No diagnostic output.",
  ].join("\n");
}

export function textDownloadUrl(value: string): string {
  return `data:text/plain;charset=utf-8,${encodeURIComponent(value)}`;
}
