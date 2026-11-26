/**
 * The router state the create page passes to the workspace when the member chose to start the server after
 * creating it (D-07).
 *
 * The state carries an intent only. The workspace takes the server id from the route and still asks the member to
 * confirm the start through the shared start confirmation.
 */
export const createStartState = Object.freeze({ createdStartPending: true as const });

/**
 * Tells whether a router state carries the start intent of {@link createStartState}.
 *
 * The state is untrusted because history entries can be forged on the same origin: only an object whose own
 * property `createdStartPending` is the literal `true` qualifies. Truthy look-alikes and inherited properties do
 * not.
 *
 * @param state The `location.state` value of a router location.
 * @returns True when the state is a non-array object with an own `createdStartPending` property that is `true`.
 */
export function hasCreateStartIntent(state: unknown): boolean {
  if (typeof state !== "object" || state === null || Array.isArray(state)) return false;
  return (
    Object.hasOwn(state, "createdStartPending") &&
    (state as { createdStartPending: unknown }).createdStartPending === true
  );
}
