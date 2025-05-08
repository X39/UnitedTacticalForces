namespace X39.UnitedTacticalForces.WebApp;

public record BaseUrl(string ApiUrl, string SelfUrl)
{
    public Uri ApiUri => new (ApiUrl);
    public Uri SelfUri => new (SelfUrl);
    public string ResolveApiUrl(string path) => ResolveApiUri(path).ToString();

    public Uri ResolveApiUri(string path)
    {
        path = path.TrimStart('/');
        return new Uri(ApiUri, path);
    }

    public string ResolveSelfUrl(string path) => ResolveSelfUri(path).ToString();

    public Uri ResolveSelfUri(string path)
    {
        path = path.TrimStart('/');
        return new Uri(SelfUri, path);
    }

    public string ResolveResourceUrl(string path) => ResolveSelfUrl(path);
}
