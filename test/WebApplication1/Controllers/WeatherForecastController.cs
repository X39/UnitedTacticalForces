using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.Controllers;

[Authorize]
[ApiController]
[Route("[controller]/[action]/{testId:int}")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WeatherForecastController> _logger;

    public WeatherForecastController(ILogger<WeatherForecastController> logger)
    {
        _logger = logger;
    }

    [HttpGet(Name = "GetWeatherForecast1")]
    public IEnumerable<WeatherForecast> Get1([FromRoute] int testId)
    {
        return Enumerable.Range(1, 5)
            .Select(
                index => new WeatherForecast
                {
                    Date         = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary      = Summaries[Random.Shared.Next(Summaries.Length)]
                })
            .ToArray();
    }

    [HttpGet(Name = "GetWeatherForecast2")]
    [Authorize(Roles = "RoleAdmin")]
    public IEnumerable<WeatherForecast> Get2([FromRoute] int testId)
    {
        return Enumerable.Range(1, 5)
            .Select(
                index => new WeatherForecast
                {
                    Date         = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary      = Summaries[Random.Shared.Next(Summaries.Length)]
                })
            .ToArray();
    }

    [HttpGet(Name = "GetWeatherForecast3")]
    [Authorize("PolicyAdmin")]
    public IEnumerable<WeatherForecast> Get3([FromRoute] int testId)
    {
        return Enumerable.Range(1, 5)
            .Select(
                index => new WeatherForecast
                {
                    Date         = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary      = Summaries[Random.Shared.Next(Summaries.Length)]
                })
            .ToArray();
    }
}