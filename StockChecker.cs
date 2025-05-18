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
        private Logger _logger;
        private string _url { get; set; }
        private List<string> _div { get; set; }
        private List<List<string>> _id { get; set; }
        private string _checker { get; set; }
        private CheckType _checkType { get; set; }
        private string _alias { get; set; }
        private int _refresh { get; set; }
        private int _cooldown { get; set; }
        private SearchMode _mode { get; set; }
        private Task _task { get; set; }
        private CancellationTokenSource _cts { get; set; }
        private RestSender _webhook { get; set; }


        public StockChecker(SiteConfig config, CheckType type = CheckType.Unavailable)
        {
            this._url = config.Url;
            this._div = config.Div;
            this._id = config.Id;
            this._checker = config.CheckString;
            this._alias = config.Name;
            this._mode = config.SearchMode;
            this._refresh = config.RefreshTime;
            this._cooldown = config.InStockCooldownTime;
            // TODO Make this checktype part of config
            this._checkType = type;            
            this._cts = new CancellationTokenSource();
            this._task = Task.CompletedTask;

            var payloadUrl = string.IsNullOrEmpty(config.WebhookConfig.PayloadUrl) ? config.Url : config.WebhookConfig.PayloadUrl;
            var payloadTitle = string.IsNullOrEmpty(config.WebhookConfig.PayloadTitle) ? "Stock available" : config.WebhookConfig.PayloadTitle;
            var payloadBody = string.IsNullOrEmpty(config.WebhookConfig.PayloadBody) ? config.Name : config.WebhookConfig.PayloadBody;

            this._webhook = new RestSender(config.WebhookConfig, payloadUrl, payloadTitle, payloadBody);
            _logger = new Logger(RuntimeHelpers.GetHashCode(config));
            LogConfig(config);
        }


        public void StartService()
        {
            if (_task == null || _task.IsCompleted)
            {
                _task = Task.Run(async () =>
                {
                    int refresh;
                    while (!_cts.Token.IsCancellationRequested)
                    {
                        refresh = _refresh;
                        if (await IsAvailable())
                        {
                            refresh = _cooldown;
                            var minstr = refresh > 60 ? $" ({refresh / 60.0:F1} mins)" : "";
                            _logger.Log("".PadLeft(65, '='));
                            _logger.Log($"{_alias} Is Available! Sending notification message to home assistant");
                            _logger.Log($"Checking again in {refresh} seconds{minstr}");
                            _logger.Log("".PadLeft(65, '='));
                            _webhook.Notify();
                        }
                        else
                        {
                            var minstr = refresh > 60 ? $" ({refresh / 60.0:F1} mins)" : "";
                            _logger.Log($"{_alias} Not available. Trying again in {refresh} seconds{minstr}");
                        }
                        await Task.Delay(refresh * 1000, _cts.Token);
                    }
                }, _cts.Token);
            }
        }


        public void StopService() => _cts.Cancel();


        public async Task<bool> IsAvailable()
        {
            bool result = false;
            _logger.Log($"Attempting to check url{(_alias != "" ? $" for {_alias} " : "")}: {_url}");
            try
            {
                string response = await GetHTMLAsync2(_url);

                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(response);

                // function to reuse
                Func<string, bool> funcCheck = node =>
                {
                    bool ret = false;
                    var elements = htmlDoc.DocumentNode.SelectNodes(node);
                    if (elements == null)
                        throw new Exception($"Could not find in response '{node}'");

                    foreach (var element in elements)
                    {
                        // Check if we can find _checker inside our innerhtml
                        ret = element.OuterHtml.Contains(_checker, StringComparison.OrdinalIgnoreCase);

                        // If our search type is 'Unavailable' then we invert our result
                        ret = _checkType == CheckType.Unavailable ? !ret : ret;

                        // We are done if we found something
                        if (ret) return ret;
                    }
                    return ret;
                };

                if (_mode == SearchMode.DivClass)
                {
                    foreach (var div in _div)
                    {
                        string nodes = $"//div[contains(@class, '{div}')]";
                        result = funcCheck(nodes);
                        if (result) break;
                    }
                }
                else
                {
                    foreach (var ids in _id)
                    {
                        string nodes = $"//*[{string.Join(" and ", ids.Select(s => $"contains(@id, '{s}')"))}]";
                        result = funcCheck(nodes);
                        if (result) break;
                    }
                }
            }
            catch (Exception ex) 
            {
                _logger.Log($"Error: {ex}");

                result = false;
            }

            return result;
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
                _logger.Log($"{prop.Name.PadRight(14, ' ')}:{val}");

                var cconfig = (WebhookConfig)(prop.GetValue(obj, null) ?? new WebhookConfig());
                foreach (var pprop in cconfig.GetType().GetProperties())
                {
                    LogProps(pprop, cconfig, 3);
                }
            }
            if (doLog)
                _logger.Log($"{"".PadLeft(offset) + (prop.Name.PadRight(14, ' '))}:{val}");

        }


        public void LogConfig(SiteConfig config)
        {
            _logger.LogPadCenter("Loading Site Configuration", 70, '*');

            foreach (var prop in config.GetType().GetProperties())
            {
                LogProps(prop, config);
            }
        }


        public static async Task<string> GetHTMLAsync(string url)
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", @"Mozilla/5.0 (compatible; Rigor/1.0.0; http://rigor.com)");
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await client.SendAsync(request);

            if (response != null && response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            throw new Exception($"Request to {url} failed.");
        }


        public async Task<string> GetHTMLAsync2(string url)
        {
            using (var pw = await Playwright.CreateAsync())
            {
                await using var browser = await pw.Firefox.LaunchAsync(new()
                {
                    Headless = true,
                });
                var page = await browser.NewPageAsync();
                await page.GotoAsync(_url);
                //await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // Generate selector
                string selector = "";
                if (_mode == SearchMode.DivClass)
                {
                    // OR each div class
                    selector = $"//div[{string.Join(" or ", _div.Select(div => $"contains(@class, '{div}')"))}]";
                }
                else
                {
                    // Join AND the interior list of ids and then OR the outside list 
                    selector = $"//*[{string.Join(" or ", _id.Select(ids => $"({string.Join(" and ", ids.Select(s => $"contains(@id, '{s}')"))})"))}]";
                }
                await page.WaitForSelectorAsync(selector);
                string result = await page.ContentAsync();
                return result;  
            }
            throw new Exception($"Request to {url} failed.");
        }

    }
 }
