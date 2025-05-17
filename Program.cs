using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace ShopStockNotifier
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json", optional: false)
                .Build();
            var config = configBuilder.Get<Configuration>();

            if (config == null) throw new Exception("Configuration file invalid");

            
            foreach (var site in config.SiteConfig)
            {
                //var stock = new StockChecker(s.Url, s.Div, s.Id, s.CheckString, s.SearchMode, alias: s.Name, refresh: 30);
                var stock = new StockChecker(site);
                stock.StartService();
            }
            
            await Task.Delay(Timeout.Infinite);
        }
    }
}
