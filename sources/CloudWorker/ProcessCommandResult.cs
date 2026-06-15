namespace X39.UnitedTacticalForces.CloudWorker;

public sealed record ProcessCommandResult(bool Succeeded, int ExitCode, string Output, string Error);