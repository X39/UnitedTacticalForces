import { ArmaPlacementField } from "./ArmaPlacementField";
import type { ConfigurationContribution } from "./ConfigurationContribution";

export const armaContribution: ConfigurationContribution = {
  fields: { headlessClientPlacement: ArmaPlacementField },
  showPlacementEstimate: true,
};
