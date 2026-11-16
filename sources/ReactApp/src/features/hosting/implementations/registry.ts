import { armaContribution } from "./arma";
import type { ConfigurationContribution } from "./ConfigurationContribution";

const contributions: Readonly<Record<string, ConfigurationContribution>> = {
  arma3: armaContribution,
};

/** Returns optional field renderers for a stable game implementation identifier. */
export function configurationContribution(gameIdentifier: string | undefined) {
  const key = gameIdentifier?.toLowerCase() ?? "";
  return Object.hasOwn(contributions, key) ? contributions[key] : undefined;
}

export function configurationContributionField(
  contribution: ConfigurationContribution | undefined,
  key: string,
) {
  return contribution && Object.hasOwn(contribution.fields, key)
    ? contribution.fields[key]
    : undefined;
}
