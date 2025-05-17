using System;
using System.Text;
using System.Text.Json;

namespace ShopStockNotifier
{
    public class RestSender
    {
        private string _url { get; set; }
        private string _token { get; set; }
        private HttpClient _httpClient { get; set; }
        private StringContent _payload { get; set; }

        public RestSender(WebhookConfig config, string payloadUrl, string payloadTitle, string payloadMessage)
        {
            this._url = config.WebhookUrl;
            this._token = config.BearerToken;
            this._httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(_token))
            {
                this._httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            }

            var payload = new
            {
                url = payloadUrl,
                title = payloadTitle,
                message = payloadMessage
            };
            _payload = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        }


        public void Notify()
        {
            if (!string.IsNullOrEmpty(_url))
                _httpClient.PostAsync(_url, _payload);
        }
    }
}
