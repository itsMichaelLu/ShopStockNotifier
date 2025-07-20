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
        private static System.Timers.Timer? memoryLogTimer;

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
            var browserType = (args.Length > 0 && string.Equals(args[0], "Chrome", StringComparison.OrdinalIgnoreCase))
                ? Browsers.Chrome
                : Browsers.Firefox;
            logger.Log($"Loading Browser: {browserType.ToString()}");

            using var playwright = await Playwright.CreateAsync();
            await using var browser = browserType == Browsers.Chrome
                ? await playwright.Chromium.LaunchAsync(new() { Headless = true })
                : await playwright.Firefox.LaunchAsync(new() { Headless = true });

            logger.Log($"Browser Loaded");

            // Create all instances
            logger.Log("Loading Stock checking instances");
            stocks = config.SiteConfig.Select(site => new StockChecker(site, browser)).ToList();
            logger.Log("Stock checking instances Loaded");

            // Run them all
            logger.Log("Starting services...");
            stocks.ForEach(stock => stock.StartService());
            logger.Log("Services started");

            // Start a timer to print memory usage
            memoryLogTimer = new System.Timers.Timer
            {
                Interval = 30 * 1000,
                AutoReset = true,
                Enabled = true
            };
            memoryLogTimer.Elapsed += (_, __) => PrintMemoryUsage();
            logger.Log("Started memory tracker");

            await Task.Delay(Timeout.Infinite);
        }

        public enum Browsers
        {
            Chrome,
            Firefox
        }

        private static void PrintMemoryUsage()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            long memoryBytes = process.WorkingSet64;
            double memoryMB = memoryBytes / (1024.0 * 1024.0);
            logger.Log($"[Memory] Working Set: {memoryMB:F2} MB");
            logger.Log($"[GC] TotalMemory: {GC.GetTotalMemory(false) / (1024.0 * 1024.0):F2} MB");
        }

        private static void Shutdown()
        {
            logger.Log("Shutting down, please wait...");

            // Stop services
            stocks?.ForEach(stock => stock.StopService());

            // Stop out memory log timer
            memoryLogTimer?.Stop();
            memoryLogTimer?.Dispose();

            // Remove handler
            logger.Log("Shutdown complete.");
        }
    }
}
