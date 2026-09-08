const missionObjectiveHeadingPattern = /^\s{0,3}#{1,6}\s+mission objectives\s*$/i;
const missionObjectiveLinePattern =
  /^\s*(?:(?:[-*+]\s+|\d+[.)]\s+)(?::[a-z0-9-]+:\s*)?\S.*|:[a-z0-9-]+:\s*\S.*)\s*$/i;

function normalizedLines(value: string): string[] {
  return value.replace(/\r\n/g, "\n").replace(/\r/g, "\n").split("\n");
}

/**
 * Removes the leading, authoring-generated mission-objective block while preserving the full
 * briefing body that follows it.
 */
export function removeObjectiveBlock(description: string | null | undefined): string {
  if (!description?.trim()) return "";

  const lines = normalizedLines(description);
  const firstContent = lines.findIndex((line) => line.trim().length > 0);
  if (firstContent < 0 || !missionObjectiveHeadingPattern.test(lines[firstContent] ?? "")) {
    return description;
  }

  let index = firstContent + 1;
  while (index < lines.length && !lines[index]?.trim()) index += 1;
  while (index < lines.length && missionObjectiveLinePattern.test(lines[index] ?? "")) index += 1;
  while (index < lines.length && !lines[index]?.trim()) index += 1;
  return lines.slice(index).join("\n").trimStart();
}
