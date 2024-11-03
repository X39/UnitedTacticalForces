namespace X39.UnitedTacticalForces.WebApp.Services;

public class RepositoryBase
{
    protected Client Client { get; }

    public RepositoryBase(HttpClient httpClient, BaseUrl baseUrl)
    {
        Client = new Client(baseUrl.ApiUrl, httpClient);
    }
}