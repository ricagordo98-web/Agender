using System.Text;
using System.Text.Json;

namespace Agender.Infrastructure.Services;

public class WhatsAppSender(HttpClient http)
{
    private const int MaxErrorBody = 160;

    public async Task SendAsync(string apiUrl, string instance, string apiKey, string destination, string text, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, $"{apiUrl.TrimEnd('/')}/message/sendText/{Uri.EscapeDataString(instance)}");
        request.Headers.Add("apikey", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(new { number = destination, text }), Encoding.UTF8, "application/json");

        HttpResponseMessage response;

        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ArgumentException($"Não foi possível ligar à Evolution API em {apiUrl} ({ex.Message}).", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ArgumentException("A Evolution API não respondeu a tempo.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                body = body.Length > MaxErrorBody ? body[..MaxErrorBody] + "…" : body;
                throw new ArgumentException($"A Evolution API recusou a mensagem ({(int)response.StatusCode}). {body}".Trim());
            }
        }
    }
}
