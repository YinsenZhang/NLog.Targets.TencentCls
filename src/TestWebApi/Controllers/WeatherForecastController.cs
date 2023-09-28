using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace TestWebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
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

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            Dictionary<string, string> map = new Dictionary<string, string>()
            {
                {"testName","value" },
                {"testName1","value1" },
                {"testName2","value2" },
                {"testName3","value3" },
                {"testName4","value4" },
            };
            var log = JsonConvert.SerializeObject(map);
            _logger.LogInformation(log);
            try
            {
                throw new Exception("Exception Test");
            }
            catch (Exception ex)
            {
                //_logger.LogError("server err", ex);
            }
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}