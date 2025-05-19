using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace ShopStockNotifier
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            Logger logger = new Logger();

            logger.LogHeader("Loading Configuration");

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

            // Create browser
            IPlaywright playwright = await Playwright.CreateAsync();
            IBrowser browser = args.Length > 0 && string.Equals(args[0], "Chrome", StringComparison.OrdinalIgnoreCase)
                ? await playwright.Chromium.LaunchAsync(new() { Headless = true })
                : await playwright.Firefox.LaunchAsync(new() { Headless = true });
            // Dispose browser on exit
            AppDomain.CurrentDomain.ProcessExit += async (_, __) => await CleanupAsync(playwright, browser);
            Console.CancelKeyPress += async (_, __) => await CleanupAsync(playwright, browser);

            // Create all instances
            var stocks = config.SiteConfig.Select(site => new StockChecker(site, browser)).ToList();
            logger.LogHeader("Configuration Loaded");

            // Run them all
            foreach (var stock in stocks)
            {
                stock.StartService();
            }

            await Task.Delay(Timeout.Infinite);
        }
        private static async Task CleanupAsync(IPlaywright p, IBrowser b)
        {
            try
            {
                if (b != null)
                    await b.CloseAsync();
                if (p != null)
                    p.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cleanup error: {ex.Message}");
            }
        }

    }
}
