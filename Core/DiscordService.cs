using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MatrixHole.Core
{
    public static class DiscordService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<bool> SendMessageAsync(string webhookUrl, string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(webhookUrl) || string.IsNullOrWhiteSpace(text))
                    return false;

                var payload = new { content = text };
                var json = JsonConvert.SerializeObject(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(webhookUrl, content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<bool> SendEmbedAsync(string webhookUrl, string title, string description,
            int color, (string name, string value, bool inline)[] fields, string? footer = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(webhookUrl))
                    return false;

                var embedFields = fields.Select(f => new { name = f.name, value = f.value, inline = f.inline }).ToArray();
                var embed = new
                {
                    title,
                    description,
                    color,
                    fields = embedFields,
                    footer = string.IsNullOrEmpty(footer) ? null : new { text = footer },
                    timestamp = DateTime.UtcNow.ToString("O")
                };
                var payload = new { embeds = new[] { embed } };
                var json = JsonConvert.SerializeObject(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(webhookUrl, content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<bool> SendFileAsync(string webhookUrl, string filePath, string message = "")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(webhookUrl) || !File.Exists(filePath))
                    return false;

                using var content = new MultipartFormDataContent();

                if (!string.IsNullOrEmpty(message))
                {
                    var payload = new { content = message };
                    content.Add(new StringContent(JsonConvert.SerializeObject(payload)), "payload_json");
                }

                var fileBytes = File.ReadAllBytes(filePath);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
                content.Add(fileContent, "file", Path.GetFileName(filePath));

                var response = await _httpClient.PostAsync(webhookUrl, content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}
