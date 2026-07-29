namespace X39.UnitedTacticalForces.GameServerImplementations;

public sealed record RenderedConfigurationFile(
    string Path,
    byte[] Content,
    bool Sensitive);
