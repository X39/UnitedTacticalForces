/** The number of matches the pickers of the hosting pages show. */
export const pickerResultLimit = 20;

/**
 * The number of matches a picker search requests. One more than is shown, so a full answer tells the picker that more
 * matches exist and it can say so instead of looking complete. The API allows up to 50.
 */
export const pickerRequestTake = pickerResultLimit + 1;

/**
 * Cuts a picker answer down to the shown matches and reports whether the answer held more.
 *
 * @param items The answer of a search that requested {@link pickerRequestTake} matches.
 * @returns The first {@link pickerResultLimit} matches and whether the answer was longer than that.
 */
export function limitPickerResults<T>(items: readonly T[]): { items: T[]; hasMore: boolean } {
  return { items: items.slice(0, pickerResultLimit), hasMore: items.length > pickerResultLimit };
}
