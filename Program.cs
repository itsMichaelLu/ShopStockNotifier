using System.Linq;
using System.Runtime.ConstrainedExecution;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace ShopStockNotifier
{
    static class Program
    {
        private static List<StockChecker> stocks = null!;
        private static readonly Logger logger = new Logger();

        static async Task Main(string[] args)
        {
            // Creating shutdown event before we begin
            AppDomain.CurrentDomain.ProcessExit += (_, __) => Shutdown();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Shutdown();
            };

            logger.Log("Loading Configuration");

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json", optional: false)
                .Build();
            var config = configBuilder.Get<Configuration>();

            if (config == null)
            {
                logger.Log("Configuration file invalid");
                return;
            }
            logger.Log("Configuration Loaded");

            // Create browser
            logger.Log("Loading Browser");
            var browserEnum = (args.Length > 0 && string.Equals(args[0], "Chrome", StringComparison.OrdinalIgnoreCase))
                ? Browsers.Chrome
                : Browsers.Firefox;

            using var playwright = await Playwright.CreateAsync();
            await using var browser = browserEnum == Browsers.Chrome
                ? await playwright.Chromium.LaunchAsync(new() { Headless = true })
                : await playwright.Firefox.LaunchAsync(new() { Headless = true });
            var browserContext = await browser.NewContextAsync();


            logger.Log("Browser Loaded");

            // Create all instances
            logger.Log("Loading Stock checking instances");
            stocks = config.SiteConfig.Select(site => new StockChecker(site, browserContext)).ToList();
            logger.Log("Stock checking instances Loaded");

            // Run them all
            logger.LogHeader("Starting services...");
            stocks.ForEach(stock => stock.StartService());


            await Task.Delay(Timeout.Infinite);
        }

        public enum Browsers
        {
            Chrome,
            Firefox
        }

        private static void Shutdown()
        {
            logger.Log("Shutting down, please wait...");

            // Stop services
            stocks?.ForEach(stock => stock.StopService());

            // Remove handler
            logger.Log("Shutdown complete.");
        }
    }
}
