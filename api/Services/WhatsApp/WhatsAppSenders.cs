using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Crm.Api.Services.WhatsApp;

public record SendResult(bool Ok, string? ProviderMessageId, string? Error);

public interface IWhatsAppSender
{
    Task<SendResult> SendAsync(string toPhoneE164, string body, CancellationToken ct = default);
}

/// <summary>Default dev provider: logs the message instead of sending it. Everything still lands in WhatsAppMessages.</summary>
public class LoggingWhatsAppSender(ILogger<LoggingWhatsAppSender> logger) : IWhatsAppSender
{
    public Task<SendResult> SendAsync(string toPhoneE164, string body, CancellationToken ct = default)
    {
        logger.LogInformation("[WhatsApp SIMULATED] to={Phone}: {Body}", toPhoneE164, body.ReplaceLineEndings(" "));
        return Task.FromResult(new SendResult(true, $"sim-{Guid.NewGuid():N}", null));
    }
}

/// <summary>Meta WhatsApp Cloud API sender. Configure WhatsApp:MetaCloud:AccessToken + PhoneNumberId.</summary>
public class MetaCloudWhatsAppSender(HttpClient http, IConfiguration config, ILogger<MetaCloudWhatsAppSender> logger) : IWhatsAppSender
{
    private readonly string? _token = config["WhatsApp:MetaCloud:AccessToken"];
    private readonly string? _phoneId = config["WhatsApp:MetaCloud:PhoneNumberId"];
    private readonly string _apiVersion = config["WhatsApp:MetaCloud:ApiVersion"] ?? "v21.0";

    public async Task<SendResult> SendAsync(string toPhoneE164, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_token) || string.IsNullOrWhiteSpace(_phoneId))
            return new SendResult(false, null, "Meta Cloud API not configured (missing AccessToken/PhoneNumberId)");

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{_apiVersion}/{_phoneId}/messages");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            req.Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = toPhoneE164.TrimStart('+'),
                type = "text",
                text = new { preview_url = false, body }
            });
            using var res = await http.SendAsync(req, ct);
            var raw = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                return new SendResult(false, null, $"HTTP {(int)res.StatusCode}: {raw}");

            using var doc = JsonDocument.Parse(raw);
            var msgId = doc.RootElement.GetProperty("messages")[0].GetProperty("id").GetString();
            return new SendResult(true, msgId, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Meta WhatsApp send failed");
            return new SendResult(false, null, ex.Message);
        }
    }
}
