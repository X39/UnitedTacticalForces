namespace X39.UnitedTacticalForces.Api.Services.GameServerController;

internal static class PathUtils
{

    public static string PathRelativeTo(this string self, string target)
    {
        var pathSplatted = self.Split('/', '\\');
        var targetSplatted = target.Split('/', '\\');
        var matchingPathSegments = 0;
        for (var i = 0; i < targetSplatted.Length; i++)
        {
            if (pathSplatted.Length > i && targetSplatted[i] == pathSplatted[i])
                matchingPathSegments++;
            else
                break;
        }

        var relativeUp = Enumerable.Range(0, targetSplatted.Length - matchingPathSegments).Select(_ => "..");
        var relativeDown = pathSplatted.Skip(matchingPathSegments);
        return Path.Combine(relativeUp.Concat(relativeDown).ToArray());
    }
}