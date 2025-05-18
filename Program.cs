using Microsoft.Extensions.Configuration;

namespace ShopStockNotifier
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Logger logger = new Logger();

            logger.LogPadCenter("Loading Configuration", 70, '=');

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json", optional: false)
                .Build();
            var config = configBuilder.Get<Configuration>();

            if (config == null) throw new Exception("Configuration file invalid");

            // Create all instances
            var stocks = config.SiteConfig.Select(site => new StockChecker(site)).ToList();
            logger.LogPadCenter("Configuration Loaded", 70, '=');

            // Run them all
            foreach (var stock in stocks)
            {
                stock.StartService();
            }

            await Task.Delay(Timeout.Infinite);
        }
    }
}
