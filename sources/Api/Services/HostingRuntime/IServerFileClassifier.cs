using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public interface IServerFileClassifier
{
    EFileClassification Classify(string relativePath, bool isDirectory);

    bool IsWritable(EFileContext context, EFileClassification classification);
}
