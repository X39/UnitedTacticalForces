import type { ComponentType } from "react";

/** Shared field context; implementation-specific keys stay in their contribution. */
export interface ConfigurationContributionProps {
  id: string;
  value: string;
  disabled?: boolean;
  required?: boolean;
  allowUnset?: boolean;
  invalid?: boolean;
  describedBy?: string;
  onChange(value: string): void;
}

export interface ConfigurationContribution {
  showPlacementEstimate?: boolean;
  fields: Readonly<Record<string, ComponentType<ConfigurationContributionProps>>>;
}
