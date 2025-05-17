using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShopStockNotifier
{

    public class Configuration
    {
        public required List<SiteConfig> SiteConfig { get; set; }
    }
    public class SiteConfig
    {
        public required string Name { get; set; }
        public required string Url { get; set; }
        public int RefreshTime { get; set; } = 30;
        public required SearchMode SearchMode { get; set; }
        public List<string> Div { get; set; } = new List<string>();
        public List<List<string>> Id { get; set; } = new List<List<string>>();
        public required string CheckString { get; set; }
        public required WebhookConfig WebhookConfig { get; set; }

    }
    public class WebhookConfig
    {
        public string WebhookUrl { get; set; } = "";
        public string BearerToken { get; set; } = "";
        public string PayloadTitle { get; set; } = "";
        public string PayloadBody { get; set; } = "";
        public string PayloadUrl { get; set; } = "";

    }

}
