namespace X39.UnitedTacticalForces.Api.Services;

public record BaseUrl(string ApiUrl, string ClientUrl)
{
    public Uri ApiUri => new (ApiUrl);
    public Uri ClientUri => new (ClientUrl);
    public string ResolveApiUrl(string path) => ResolveApiUri(path).ToString();

    public Uri ResolveApiUri(string path)
    {
        path = path.TrimStart('/');
        return new Uri(ApiUri, path);
    }

    public string ResolveClientUrl(string path) => ResolveClientUri(path).ToString();

    public Uri ResolveClientUri(string path)
    {
        path = path.TrimStart('/');
        return new Uri(ClientUri, path);
    }
}