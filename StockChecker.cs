using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using HtmlAgilityPack;
using Microsoft.Playwright;

namespace ShopStockNotifier
{
    public enum CheckType
    {
        Unavailable = 0,
        Available
    }

    public enum SearchMode
    {
        DivClass = 0,
        Id
    }

    internal class StockChecker
    {
        private Logger logger;
        private string url { get; set; }
        private List<string> divList { get; set; }
        private List<List<string>> idList { get; set; }
        private string checker { get; set; }
        private CheckType checkType { get; set; }
        private string productName { get; set; }
        private int refreshTime { get; set; }
        private int cooldownTime { get; set; }
        private SearchMode searchMode { get; set; }
        private Task task { get; set; }
        private CancellationTokenSource cts { get; set; }
        private RestSender webhook { get; set; }


        public StockChecker(SiteConfig config, CheckType type = CheckType.Unavailable)
        {
            // Create logger with a 'unique' hash for this instance
            logger = new Logger(RuntimeHelpers.GetHashCode(this));

            this.url = config.Url;
            this.divList = config.Div;
            this.idList = config.Id;
            this.checker = config.CheckString;
            this.productName = config.Name;
            this.searchMode = config.SearchMode;
            this.refreshTime = config.RefreshTime;
            this.cooldownTime = config.InStockCooldownTime;
            // TODO Make this checktype part of config
            this.checkType = type;            
            this.cts = new CancellationTokenSource();
            this.task = CreateTask(cts.Token);

            var payloadUrl = string.IsNullOrEmpty(config.WebhookConfig.PayloadUrl) ? config.Url : config.WebhookConfig.PayloadUrl;
            var payloadTitle = string.IsNullOrEmpty(config.WebhookConfig.PayloadTitle) ? "Stock available" : config.WebhookConfig.PayloadTitle;
            var payloadBody = string.IsNullOrEmpty(config.WebhookConfig.PayloadBody) ? config.Name : config.WebhookConfig.PayloadBody;

            this.webhook = new RestSender(config.WebhookConfig, payloadUrl, payloadTitle, payloadBody);

            LogConfig(config);
        }


        public void StartService() => task.Start();


        public void StopService() => cts.Cancel();


        private Task CreateTask(CancellationToken token)
        {
            return new Task(async () =>
            {
                int refresh;
                while (!cts.Token.IsCancellationRequested)
                {
                    refresh = refreshTime;
                    if (await IsAvailable())
                    {
                        refresh = cooldownTime;
                        var minstr = refresh > 60 ? $" ({refresh / 60.0:F1} mins)" : "";
                        logger.Log("".PadLeft(65, '='));
                        logger.Log($"Stock Available!!!: [{productName}] at URL [{url}]. Sending notification message to webhook");
                        logger.Log($"Checking again in {refresh} seconds{minstr}");
                        logger.Log("".PadLeft(65, '='));
                        webhook.Notify();
                    }
                    else
                    {
                        var minstr = refresh > 60 ? $" ({refresh / 60.0:F1} mins)" : "";
                        logger.Log($"NOT available: [{productName}] Trying again in {refresh} seconds{minstr}");
                    }
                    await Task.Delay(refresh * 1000, cts.Token);
                }
            });
        }


        private async Task<bool> IsAvailable()
        {
            bool result = false;

            logger.Log($"Checking for [{productName}] at URL [{url}]");
            try
            {
                string response = await GetHTMLAsync(url);

                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(response);

                // Inline function to search a node
                Func<string, bool> funcCheck = node =>
                {
                    bool ret = false;
                    var elements = htmlDoc.DocumentNode.SelectNodes(node);
                    if (elements == null)
                        throw new Exception($"Could not find in response '{node}'");

                    foreach (var element in elements)
                    {
                        // Check if we can find _checker inside our innerhtml
                        ret = element.OuterHtml.Contains(checker, StringComparison.OrdinalIgnoreCase);

                        // If our search type is 'Unavailable' then we invert our result
                        ret = checkType == CheckType.Unavailable ? !ret : ret;

                        // We are done if we found something
                        if (ret) return ret;
                    }
                    return ret;
                };

                if (searchMode == SearchMode.DivClass)
                {
                    foreach (var div in divList)
                    {
                        string nodes = $"//div[contains(@class, '{div}')]";
                        result = funcCheck(nodes);
                        if (result) break;
                    }
                }
                else
                {
                    foreach (var ids in idList)
                    {
                        // AND all of the inner ID's
                        string nodes = $"//*[{string.Join(" and ", ids.Select(s => $"contains(@id, '{s}')"))}]";
                        result = funcCheck(nodes);
                        if (result) break;
                    }
                }
            }
            catch (Exception ex) 
            {
                logger.Log($"Error: {ex}");

                result = false;
            }

            return result;
        }


        private async Task<string> GetHTMLAsync(string productUrl)
        {
            using (var pw = await Playwright.CreateAsync())
            {
                await using var browser = await pw.Firefox.LaunchAsync(new()
                {
                    Headless = true,
                });
                var page = await browser.NewPageAsync();
                await page.GotoAsync(productUrl);
                //await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // Generate selector
                string selector = "";
                if (searchMode == SearchMode.DivClass)
                {
                    // OR each div class
                    selector = $"//div[{string.Join(" or ", divList.Select(div => $"contains(@class, '{div}')"))}]";
                }
                else
                {
                    // Join AND the interior list of ids and then OR the outside list 
                    selector = $"//*[{string.Join(" or ", idList.Select(ids => $"({string.Join(" and ", ids.Select(s => $"contains(@id, '{s}')"))})"))}]";
                }
                await page.WaitForSelectorAsync(selector);
                string result = await page.ContentAsync();
                return result;  
            }
            throw new Exception($"Request to {productUrl} failed.");
        }


        private void LogProps(PropertyInfo prop, Object obj, int offset = 0)
        {
            bool doLog = true;
            var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            string val = "";
            if (type == typeof(string))
            {
                //_logger.Log($"{prop.Name.PadRight(20, ' ')}:{prop.GetValue(config,null)?.ToString()}");
                val = prop.GetValue(obj, null)?.ToString() ?? "";
            }
            else if (type == typeof(int))
            {
                val = prop.GetValue(obj, null)?.ToString() ?? "";
            }
            else if (type == typeof(List<string>))
            {
                var tmp = (List<string>)(prop.GetValue(obj, null) ?? new List<string>());
                val = $"[{string.Join(',', tmp)}]";
            }
            else if (type == typeof(SearchMode))
            {
                var tmp = (SearchMode)(prop.GetValue(obj, null) ?? SearchMode.DivClass);
                val = tmp.ToString();
            }
            else if (type == typeof(List<List<string>>))
            {
                var tmp = (List<List<string>>)(prop.GetValue(obj, null) ?? new List<List<string>>());
                var tmp2 = tmp.Select(x => $"[{string.Join(',', x)}]");
                val = $"[{string.Join(',', tmp2)}]";
            }
            else if (type == typeof(WebhookConfig))
            {
                // Log here instead, otherwise it will log at the end which isnt cool
                doLog = false;
                logger.Log($"{prop.Name.PadRight(14, ' ')}:{val}");

                var cconfig = (WebhookConfig)(prop.GetValue(obj, null) ?? new WebhookConfig());
                foreach (var pprop in cconfig.GetType().GetProperties())
                {
                    LogProps(pprop, cconfig, 3);
                }
            }
            if (doLog)
                logger.Log($"{"".PadLeft(offset) + (prop.Name.PadRight(14, ' '))}:{val}");

        }


        public void LogConfig(SiteConfig config)
        {
            logger.LogPadCenter("Loading Site Configuration", 70, '*');

            foreach (var prop in config.GetType().GetProperties())
            {
                LogProps(prop, config);
            }
        }
    }
}
