namespace X39.UnitedTacticalForces.WebApp.Services;

public class RepositoryBase
{
    protected Client Client { get; }

    public RepositoryBase(HttpClient httpClient)
    {
        Client = new Client("https://localhost:44387/", httpClient);
    }
}