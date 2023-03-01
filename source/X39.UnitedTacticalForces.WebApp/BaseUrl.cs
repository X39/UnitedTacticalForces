namespace X39.UnitedTacticalForces.WebApp;

public record BaseUrl(string ApiUrl, string SelfUrl)
{
    public Uri ApiUri => new (ApiUrl);
    public string ResolveApiUrl(string path) => ResolveApiUri(path).ToString();

    public Uri ResolveApiUri(string path)
    {
        path = path.TrimStart('/');
        return new Uri(ApiUri, path);
    }
}