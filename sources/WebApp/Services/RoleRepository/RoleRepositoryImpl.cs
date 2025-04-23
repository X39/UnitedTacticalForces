namespace X39.UnitedTacticalForces.WebApp.Services.RoleRepository;

public sealed class RoleRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl) : RepositoryBase(httpClient, baseUrl),
    IRoleRepository { }
