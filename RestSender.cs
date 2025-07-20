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
        private Logger _logger { get; set; }

        public RestSender(WebhookConfig config, string payloadUrl, string payloadTitle, string payloadMessage, Logger logger)
        {
            this._logger = logger;
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


        public async Task Notify()
        {
            if (string.IsNullOrEmpty(_url))
            {
                _logger.Log("Webhook url is missing. Unable to send");
                return;
            }

            try
            {
                var response = await _httpClient.PostAsync(_url, _payload);
                if (response.IsSuccessStatusCode)
                {
                    _logger.Log("Successfully sent Payload to Webhook URL");
                }
                else
                {
                    _logger.Log($"Error when sending Payload to Webhook URL: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error: {ex.Message}");
            }

        }
    }
}
